using System.Diagnostics;
using System.Linq.Expressions;
using ChangeLens.Core.Analysis.Snapshots.Models;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;
using ChangeLens.Infrastructure.LocalState.Helpers;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.AnalysisRuns.Services;

/// <summary>
///     Implements durable analysis run lifecycle storage over the local-state SQLite database.
/// </summary>
/// <param name="context">The scoped local-state context. Cannot be <see langword="null" />.</param>
/// <param name="timeProvider">The time provider used for durability-adjacent reads. Cannot be <see langword="null" />.</param>
/// <param name="logger">The analysis-store lifecycle logger. Cannot be <see langword="null" />.</param>
public sealed class SqliteAnalysisRunStore(
    ChangeLensLocalStateDbContext context,
    TimeProvider timeProvider,
    ILogger<SqliteAnalysisRunStore> logger) : IAnalysisRunStore
{
    private const string UniqueConstraintSqliteErrorMessageFragment = "UNIQUE constraint failed";

    private static readonly AnalysisRunState[] ActiveStates =
    [
        AnalysisRunState.PendingCapture,
        AnalysisRunState.Capturing,
        AnalysisRunState.Discovering,
        AnalysisRunState.Collecting,
        AnalysisRunState.Persisting,
    ];

    // Detail reads leave the reading-projection text columns unselected; only GetReadingProjectionAsync reads them.
    private static readonly Expression<Func<AnalysisRunEntity, AnalysisRunEntity>> WithoutReadingProjection = run => new AnalysisRunEntity
    {
        RunId = run.RunId,
        RepositoryId = run.RepositoryId,
        RepositoryDisplayName = run.RepositoryDisplayName,
        CanonicalRepositoryPath = run.CanonicalRepositoryPath,
        CanonicalRepositoryPathKey = run.CanonicalRepositoryPathKey,
        HeadRevision = run.HeadRevision,
        Target = run.Target,
        TargetRevision = run.TargetRevision,
        FreshnessToken = run.FreshnessToken,
        ChangeContext = run.ChangeContext,
        State = run.State,
        RequestedAtUnixMilliseconds = run.RequestedAtUnixMilliseconds,
        CaptureStartedAtUnixMilliseconds = run.CaptureStartedAtUnixMilliseconds,
        CapturedAtUnixMilliseconds = run.CapturedAtUnixMilliseconds,
        TerminalAtUnixMilliseconds = run.TerminalAtUnixMilliseconds,
        InterruptedAtUnixMilliseconds = run.InterruptedAtUnixMilliseconds,
        CancellationRequestedAtUnixMilliseconds = run.CancellationRequestedAtUnixMilliseconds,
        SnapshotId = run.SnapshotId,
        ManifestHash = run.ManifestHash,
        CapturedChangedFileCount = run.CapturedChangedFileCount,
        ExcludedUncommittedTotal = run.ExcludedUncommittedTotal,
        ExcludedStagedCount = run.ExcludedStagedCount,
        ExcludedUnstagedCount = run.ExcludedUnstagedCount,
        ExcludedUntrackedCount = run.ExcludedUntrackedCount,
        ExcludedConflictedCount = run.ExcludedConflictedCount,
        CorrespondenceCandidateCount = run.CorrespondenceCandidateCount,
        DisclosedEvidenceNodeCount = run.DisclosedEvidenceNodeCount,
        ValidationRemovalCount = run.ValidationRemovalCount,
        TerminalLimitationCount = run.TerminalLimitationCount,
        TerminalFailureCode = run.TerminalFailureCode,
        InterruptionReason = run.InterruptionReason,
    };

    /// <inheritdoc />
    public async Task<Result<AnalysisStartOutcome>> CreateOrReturnActiveAsync(
        AnalysisRunAcceptance acceptance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var repositoryId = await context.Repositories
                .Where(repository => repository.CanonicalPathKey == acceptance.CanonicalRepositoryPathKey)
                .Select(repository => (Guid?)repository.RepositoryId)
                .FirstOrDefaultAsync(cancellationToken);

            if (repositoryId is null)
            {
                logger.LogDebug(
                    "Analysis run acceptance did not find retained repository {RepositoryDisplayName}.",
                    acceptance.RepositoryDisplayName);
                logger.LogInformation(
                    "Rejected analysis run acceptance for repository {RepositoryDisplayName} because it is unavailable in "
                    + "{ElapsedMilliseconds:0.000} ms.",
                    acceptance.RepositoryDisplayName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return OperationError.NotFound("The repository is not retained in local state.", AnalysisErrorCode.RepositoryUnavailable);
            }

            var runId = Guid.NewGuid();
            var entity = new AnalysisRunEntity
            {
                RunId = runId,
                RepositoryId = repositoryId.Value,
                RepositoryDisplayName = acceptance.RepositoryDisplayName,
                CanonicalRepositoryPath = acceptance.CanonicalRepositoryPath,
                CanonicalRepositoryPathKey = acceptance.CanonicalRepositoryPathKey,
                HeadRevision = acceptance.HeadRevision,
                Target = acceptance.Target,
                TargetRevision = acceptance.TargetRevision,
                FreshnessToken = acceptance.FreshnessToken,
                ChangeContext = acceptance.ChangeContext,
                State = AnalysisRunState.PendingCapture,
                RequestedAtUnixMilliseconds = acceptance.RequestedAtUnixMilliseconds,
            };
            context.AnalysisRuns.Add(entity);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                context.Entry(entity).State = EntityState.Detached;
                var activeRunId = await context.AnalysisRuns
                    .Where(run => run.CanonicalRepositoryPathKey == acceptance.CanonicalRepositoryPathKey
                        && ActiveStates.Contains(run.State))
                    .Select(run => (Guid?)run.RunId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (activeRunId is null)
                {
                    logger.LogError(
                        "The unique repository-reservation index rejected acceptance but no active row was found after "
                        + "{ElapsedMilliseconds:0.000} ms.",
                        Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                    return OperationError.InternalError(
                        "The analysis run could not be accepted because of an unexpected conflict.",
                        AnalysisFailureCode.UnexpectedFailure);
                }

                logger.LogDebug(
                    "Unique active-run constraint matched active run {ActiveRunId} for target {Target}.",
                    activeRunId, acceptance.Target);
                logger.LogInformation(
                    "Rejected analysis run acceptance for repository {RepositoryDisplayName} because run {ActiveRunId} is already active in "
                    + "{ElapsedMilliseconds:0.000} ms.",
                    acceptance.RepositoryDisplayName, activeRunId, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return new AnalysisStartOutcome(AnalysisStartOutcomeKind.RejectedActive, null, null, activeRunId);
            }

            logger.LogDebug(
                "Persisted pending analysis run {RunId} for repository {RepositoryDisplayName} targeting {Target}.",
                runId, acceptance.RepositoryDisplayName, acceptance.Target);
            logger.LogInformation(
                "Accepted analysis run {RunId} in PendingCapture in {ElapsedMilliseconds:0.000} ms.",
                runId, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return new AnalysisStartOutcome(AnalysisStartOutcomeKind.Accepted, runId, acceptance.RequestedAtUnixMilliseconds, null);
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<AnalysisStartOutcome>(exception, nameof(this.CreateOrReturnActiveAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunDetail>> GetDetailAsync(Guid runId, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var run = await context.AnalysisRuns
                .AsNoTracking()
                .Where(entity => entity.RunId == runId)
                .Select(WithoutReadingProjection)
                .FirstOrDefaultAsync(cancellationToken);

            logger.LogDebug(
                "Queried analysis run detail for {RunId} (found: {Found}, state: {State}) in {ElapsedMilliseconds:0.000} ms.",
                runId, run is not null, run?.State, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return run is null
                ? OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun)
                : ToDetail(run);
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<AnalysisRunDetail>(exception, nameof(this.GetDetailAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunDetail?>> GetActiveByRepositoryAsync(string canonicalRepositoryPathKey, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var run = await context.AnalysisRuns
                .AsNoTracking()
                .Where(entity => entity.CanonicalRepositoryPathKey == canonicalRepositoryPathKey && ActiveStates.Contains(entity.State))
                .Select(WithoutReadingProjection)
                .FirstOrDefaultAsync(cancellationToken);

            logger.LogDebug(
                "Queried active analysis run for repository (found: {Found}, runId: {RunId}, state: {State}) in {ElapsedMilliseconds:0.000} ms.",
                run is not null, run?.RunId, run?.State, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return run is null ? null : ToDetail(run);
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<AnalysisRunDetail?>(exception, nameof(this.GetActiveByRepositoryAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid?>> TakeNextPendingAsync(CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var takeTimestamp = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var candidate = await context.AnalysisRuns
                .Where(run => run.State == AnalysisRunState.PendingCapture && run.CancellationRequestedAtUnixMilliseconds == null)
                .OrderBy(run => run.RequestedAtUnixMilliseconds)
                .ThenBy(run => run.RunId)
                .FirstOrDefaultAsync(cancellationToken);

            if (candidate is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogDebug(
                    "Polled for next pending analysis run; none available in {ElapsedMilliseconds:0.000} ms.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return (Guid?)null;
            }

            var affected = await context.AnalysisRuns
                .Where(run => run.RunId == candidate.RunId
                    && run.State == AnalysisRunState.PendingCapture
                    && run.CancellationRequestedAtUnixMilliseconds == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(run => run.State, AnalysisRunState.Capturing)
                        .SetProperty(run => run.CaptureStartedAtUnixMilliseconds, takeTimestamp),
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            if (affected == 0)
            {
                logger.LogDebug(
                    "Candidate pending analysis run {RunId} changed state before claim in {ElapsedMilliseconds:0.000} ms.",
                    candidate.RunId, elapsedMilliseconds);
                return (Guid?)null;
            }

            logger.LogDebug("Claimed pending analysis run {RunId} at {TakeTimestamp}.", candidate.RunId, takeTimestamp);
            logger.LogInformation("Took analysis run {RunId} in {ElapsedMilliseconds:0.000} ms.", candidate.RunId, elapsedMilliseconds);
            return candidate.RunId;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<Guid?>(exception, nameof(this.TakeNextPendingAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result> EstablishStepPlanAsync(
        Guid runId,
        IReadOnlyList<AnalysisRunStepPlanEntry> plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            foreach (var entry in plan)
            {
                context.AnalysisRunSteps.Add(new AnalysisRunStepEntity
                {
                    RunId = runId,
                    StepId = entry.StepId,
                    Producer = entry.Producer,
                    Capability = entry.Capability,
                    Order = entry.Order,
                    Stage = entry.Stage,
                    State = AnalysisRunStepState.Pending,
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            logger.LogDebug("Persisted {StepCount} step-plan row(s) for analysis run {RunId}.", plan.Count, runId);
            logger.LogInformation(
                "Established {StepCount} step plan entry(ies) for analysis run {RunId} in {ElapsedMilliseconds:0.000} ms.",
                plan.Count, runId, elapsedMilliseconds);
            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess(exception, nameof(this.EstablishStepPlanAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunState>> TransitionStageAsync(
        Guid runId,
        AnalysisRunState expectedCurrentState,
        AnalysisRunState nextState,
        long atUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var affected = await context.AnalysisRuns
                .Where(run => run.RunId == runId && run.State == expectedCurrentState)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(run => run.State, nextState)
                        .SetProperty(
                            run => run.AnalysisStartedAtUnixMilliseconds,
                            run => nextState == AnalysisRunState.Discovering ? atUnixMilliseconds : run.AnalysisStartedAtUnixMilliseconds),
                    cancellationToken);

            var currentState = await context.AnalysisRuns
                .Where(run => run.RunId == runId)
                .Select(run => (AnalysisRunState?)run.State)
                .FirstOrDefaultAsync(cancellationToken);

            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            if (currentState is null)
            {
                logger.LogInformation(
                    "Stage transition for unknown analysis run {RunId} finished with notFound in {ElapsedMilliseconds:0.000} ms.",
                    runId, elapsedMilliseconds);
                return OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun);
            }

            var resultState = affected == 1 ? nextState : currentState.Value;
            logger.LogDebug(
                "Executed stage transition on {RunId} expecting {ExpectedState} -> {NextState}; updated={Updated}, current={CurrentState}.",
                runId, expectedCurrentState, nextState, affected == 1, resultState);
            logger.LogInformation(
                "Stage transition for analysis run {RunId} finished in state {State} (updated: {Updated}) in {ElapsedMilliseconds:0.000} ms.",
                runId, resultState, affected == 1, elapsedMilliseconds);
            return resultState;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<AnalysisRunState>(exception, nameof(this.TransitionStageAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result> BeginStepAsync(Guid runId, string stepId, long atUnixMilliseconds, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var affected = await context.AnalysisRunSteps
                .Where(step => step.RunId == runId && step.StepId == stepId && step.State == AnalysisRunStepState.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(step => step.State, AnalysisRunStepState.Running)
                        .SetProperty(step => step.StartedAtUnixMilliseconds, atUnixMilliseconds),
                    cancellationToken);

            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            logger.LogDebug("Updated step {StepId} on analysis run {RunId} to Running; affected={Affected}.", stepId, runId, affected);
            logger.LogInformation(
                "Began step {StepId} for analysis run {RunId} (updated: {Updated}) in {ElapsedMilliseconds:0.000} ms.",
                stepId, runId, affected == 1, elapsedMilliseconds);
            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess(exception, nameof(this.BeginStepAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> FinishStepAsync(
        Guid runId,
        AnalysisRunStepOutcome outcome,
        long atUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var affected = await context.AnalysisRunSteps
                .Where(step => step.RunId == runId && step.StepId == outcome.StepId && step.State == AnalysisRunStepState.Running)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(step => step.State, outcome.State)
                        .SetProperty(step => step.Code, outcome.Code)
                        .SetProperty(step => step.FinishedAtUnixMilliseconds, atUnixMilliseconds),
                    cancellationToken);

            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            logger.LogDebug(
                "Finished step {StepId} on analysis run {RunId} with state {State} and code {Code}; affected={Affected}.",
                outcome.StepId, runId, outcome.State, outcome.Code, affected);
            logger.LogInformation(
                "Finished step {StepId} for analysis run {RunId} with state {State} (committed: {Committed}) in {ElapsedMilliseconds:0.000} ms.",
                outcome.StepId, runId, outcome.State, affected == 1, elapsedMilliseconds);
            return affected == 1;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<bool>(exception, nameof(this.FinishStepAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunDetail>> RequestCancellationAsync(Guid runId, long atUnixMilliseconds, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var affected = await context.AnalysisRuns
                .Where(run => run.RunId == runId && ActiveStates.Contains(run.State))
                .Where(run => run.CancellationRequestedAtUnixMilliseconds == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(run => run.CancellationRequestedAtUnixMilliseconds, atUnixMilliseconds),
                    cancellationToken);

            logger.LogDebug("Requested cancellation for analysis run {RunId}; affected={Affected}.", runId, affected);
            var detailResult = await this.GetDetailAsync(runId, cancellationToken);
            logger.LogInformation(
                "Handled cancellation request for analysis run {RunId} with outcome {Outcome} (updated: {Updated}) in "
                + "{ElapsedMilliseconds:0.000} ms.",
                runId, detailResult.IsSuccess ? "ok" : "error", affected == 1, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return detailResult;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<AnalysisRunDetail>(exception, nameof(this.RequestCancellationAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CommitCaptureAsync(
        Guid runId,
        SnapshotCapture capture,
        long capturedAtUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var analysis = await context.AnalysisRuns.AsNoTracking().Where(run => run.RunId == runId)
                .Select(run => new
                {
                    run.CanonicalRepositoryPathKey,
                    run.Target,
                    run.TargetRevision,
                    run.HeadRevision,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (analysis is null)
            {
                logger.LogInformation(
                    "Capture commit for unknown analysis run {RunId} finished with notFound in {ElapsedMilliseconds:0.000} ms.",
                    runId, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun);
            }

            var manifest = capture.Manifest;
            if (!StringComparer.Ordinal.Equals(analysis.CanonicalRepositoryPathKey, manifest.CanonicalRepositoryPathKey) ||
                !StringComparer.Ordinal.Equals(analysis.Target, manifest.TargetReference) ||
                !StringComparer.Ordinal.Equals(analysis.TargetRevision, manifest.TargetRevision) ||
                !StringComparer.Ordinal.Equals(analysis.HeadRevision, manifest.HeadRevision))
            {
                logger.LogError(
                    "Analysis run {RunId} rejected a capture whose manifest identity does not match its accepted run after "
                    + "{ElapsedMilliseconds:0.000} ms.",
                    runId, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return OperationError.InvalidOperation(
                    "The captured manifest does not match the accepted run identity.", AnalysisFailureCode.UnexpectedFailure);
            }

            var counts = capture.ExcludedUncommittedCounts;
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var affected = await context.AnalysisRuns
                .Where(run => run.RunId == runId
                              && run.State == AnalysisRunState.Capturing
                              && run.CapturedAtUnixMilliseconds == null
                              && run.CancellationRequestedAtUnixMilliseconds == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(run => run.CapturedAtUnixMilliseconds, capturedAtUnixMilliseconds)
                        .SetProperty(run => run.SnapshotId, manifest.SnapshotId)
                        .SetProperty(run => run.ManifestHash, manifest.ManifestHash)
                        .SetProperty(run => run.TargetRevisionAtCapture, manifest.TargetRevision)
                        .SetProperty(run => run.HeadRevisionAtCapture, manifest.HeadRevision)
                        .SetProperty(run => run.MergeBaseRevision, manifest.MergeBaseRevision)
                        .SetProperty(run => run.CapturedChangedFileCount, manifest.Entries.Count)
                        .SetProperty(run => run.ExcludedUncommittedTotal, counts.Total)
                        .SetProperty(run => run.ExcludedStagedCount, counts.Staged)
                        .SetProperty(run => run.ExcludedUnstagedCount, counts.Unstaged)
                        .SetProperty(run => run.ExcludedUntrackedCount, counts.Untracked)
                        .SetProperty(run => run.ExcludedConflictedCount, counts.Conflicted),
                    cancellationToken);

            if (affected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogInformation(
                    "Analysis run {RunId} did not commit its capture because durable state already moved on in {ElapsedMilliseconds:0.000} ms.",
                    runId, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return false;
            }

            context.SnapshotManifestEntries.AddRange(manifest.Entries.Select(entry => new SnapshotManifestEntryEntity
            {
                RunId = runId,
                Path = entry.Path,
                OriginalPath = entry.OriginalPath,
                Category = entry.Category,
                MergeBaseEntryMode = entry.MergeBaseEntryMode,
                HeadEntryMode = entry.HeadEntryMode,
                MergeBaseObjectId = entry.MergeBaseObjectId,
                HeadObjectId = entry.HeadObjectId,
            }));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogDebug("Committed snapshot {SnapshotId} manifest entries for run {RunId}.", manifest.SnapshotId, runId);
            logger.LogInformation(
                "Analysis run {RunId} committed snapshot {SnapshotId} with {EntryCount} manifest entries in {ElapsedMilliseconds:0.000} ms.",
                runId, manifest.SnapshotId, manifest.Entries.Count, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return true;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<bool>(exception, nameof(this.CommitCaptureAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<Result> RecordCorrespondenceCandidateCountAsync(Guid runId, int count, CancellationToken cancellationToken) =>
        this.RecordActiveCountAsync(
            runId,
            "correspondenceCandidates",
            count,
            setters => setters.SetProperty(run => run.CorrespondenceCandidateCount, count),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result> RecordDisclosedEvidenceNodeCountAsync(Guid runId, int count, CancellationToken cancellationToken) =>
        this.RecordActiveCountAsync(
            runId,
            "disclosedEvidenceNodes",
            count,
            setters => setters.SetProperty(run => run.DisclosedEvidenceNodeCount, count),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result> RecordValidationRemovalCountAsync(Guid runId, int count, CancellationToken cancellationToken) =>
        this.RecordActiveCountAsync(
            runId,
            "validationRemovals",
            count,
            setters => setters.SetProperty(run => run.ValidationRemovalCount, count),
            cancellationToken);

    /// <inheritdoc />
    public async Task<Result<bool>> CommitTerminalAsync(
        Guid runId,
        AnalysisTerminalSummary terminal,
        AnalysisReadingProjection? readingProjection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        var nextState = terminal.Kind switch
        {
            AnalysisTerminalKind.Completed => AnalysisRunState.Completed,
            AnalysisTerminalKind.CompletedWithLimitations => AnalysisRunState.CompletedWithLimitations,
            AnalysisTerminalKind.Cancelled => AnalysisRunState.Cancelled,
            AnalysisTerminalKind.Failed => AnalysisRunState.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(terminal)),
        };
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var readingModelJson = readingProjection?.ReadingModelJson;
            var validationRemovalsJson = readingProjection?.ValidationRemovalsJson;
            var affected = await context.AnalysisRuns
                .Where(run => run.RunId == runId && ActiveStates.Contains(run.State))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(run => run.State, nextState)
                        .SetProperty(run => run.TerminalAtUnixMilliseconds, terminal.TerminalAtUnixMilliseconds)
                        .SetProperty(run => run.TerminalLimitationCount, terminal.LimitationCount)
                        .SetProperty(run => run.TerminalFailureCode, terminal.FailureCode)
                        .SetProperty(run => run.ReadingModelJson, readingModelJson)
                        .SetProperty(run => run.ValidationRemovalsJson, validationRemovalsJson),
                    cancellationToken);

            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            logger.LogDebug(
                "Attempted terminal commit on analysis run {RunId} with state {State}, failure code {FailureCode}; affected={Affected}.",
                runId, nextState, terminal.FailureCode, affected);
            logger.LogInformation(
                "Committed terminal state {State} for analysis run {RunId} (committed: {Committed}) in {ElapsedMilliseconds:0.000} ms.",
                nextState, runId, affected == 1, elapsedMilliseconds);

            return affected == 1;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<bool>(exception, nameof(this.CommitTerminalAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisReadingProjection?>> GetReadingProjectionAsync(Guid runId, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var stored = await context.AnalysisRuns
                .AsNoTracking()
                .Where(run => run.RunId == runId)
                .Select(run => new { run.State, run.ReadingModelJson, run.ValidationRemovalsJson })
                .FirstOrDefaultAsync(cancellationToken);

            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            if (stored is null)
            {
                logger.LogInformation(
                    "Reading projection lookup for unknown analysis run {RunId} finished with notFound in {ElapsedMilliseconds:0.000} ms.",
                    runId, elapsedMilliseconds);
                return OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun);
            }

            var isCompleted = stored.State is AnalysisRunState.Completed or AnalysisRunState.CompletedWithLimitations;
            var hasProjection = isCompleted && stored.ReadingModelJson is not null && stored.ValidationRemovalsJson is not null;
            logger.LogDebug(
                "Queried reading projection for analysis run {RunId}; state={State}, hasProjection={HasProjection}.",
                runId, stored.State, hasProjection);
            logger.LogInformation(
                "Read analysis projection for run {RunId} (found: {Found}) in {ElapsedMilliseconds:0.000} ms.",
                runId, hasProjection, elapsedMilliseconds);
            return hasProjection
                ? new AnalysisReadingProjection(stored.ReadingModelJson!, stored.ValidationRemovalsJson!)
                : (AnalysisReadingProjection?)null;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<AnalysisReadingProjection?>(exception, nameof(this.GetReadingProjectionAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> FinalizeCancelledPendingRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var affected = await context.AnalysisRuns
                .Where(run => run.State == AnalysisRunState.PendingCapture && run.CancellationRequestedAtUnixMilliseconds != null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(run => run.State, AnalysisRunState.Cancelled)
                        .SetProperty(run => run.TerminalAtUnixMilliseconds, atUnixMilliseconds),
                    cancellationToken);

            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            if (affected > 0)
            {
                logger.LogInformation(
                    "Finalized {FinalizedCount} cancelled pending analysis run(s) in {ElapsedMilliseconds:0.000} ms.",
                    affected, elapsedMilliseconds);
            }
            else
            {
                logger.LogDebug(
                    "Polled for cancelled pending analysis runs; finalized {FinalizedCount} in {ElapsedMilliseconds:0.000} ms.",
                    affected, elapsedMilliseconds);
            }

            return affected;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<int>(exception, nameof(this.FinalizeCancelledPendingRunsAsync), startedAt, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> InterruptActiveRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var affected = await context.AnalysisRuns
                .Where(run => ActiveStates.Contains(run.State))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(run => run.State, AnalysisRunState.Interrupted)
                        .SetProperty(run => run.InterruptedAtUnixMilliseconds, atUnixMilliseconds)
                        .SetProperty(run => run.InterruptionReason, AnalysisInterruptionReason.EngineStopped),
                    cancellationToken);

            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            logger.LogDebug("Executed startup active-run interruption sweep; affected={InterruptedCount}.", affected);
            logger.LogInformation(
                "Startup recovery interrupted {InterruptedCount} active run(s) in {ElapsedMilliseconds:0.000} ms.",
                affected, elapsedMilliseconds);

            return affected;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess<int>(exception, nameof(this.InterruptActiveRunsAsync), startedAt, cancellationToken);
        }
    }

    private async Task<Result> RecordActiveCountAsync(
        Guid runId,
        string metricName,
        int count,
        Action<UpdateSettersBuilder<AnalysisRunEntity>> setCount,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var affected = await context.AnalysisRuns
                .Where(run => run.RunId == runId && ActiveStates.Contains(run.State))
                .ExecuteUpdateAsync(setCount, cancellationToken);

            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            logger.LogDebug(
                "Recorded {MetricName}={Count} on analysis run {RunId}; affected={Affected}.",
                metricName, count, runId, affected);
            logger.LogInformation(
                "Recorded {MetricName} count {Count} for analysis run {RunId} (updated: {Updated}) in {ElapsedMilliseconds:0.000} ms.",
                metricName, count, runId, affected == 1, elapsedMilliseconds);
            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            return this.FailAccess(exception, metricName, startedAt, cancellationToken);
        }
    }

    private Result FailAccess(
        Exception exception,
        string operation,
        long startedAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
        logger.LogWarning(
            "SQLite analysis-run store operation {Operation} failed after {ElapsedMilliseconds:0.000} ms.",
            operation, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        return LocalStateFailure.Unavailable();
    }

    private Result<T> FailAccess<T>(
        Exception exception,
        string operation,
        long startedAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
        logger.LogWarning(
            "SQLite analysis-run store operation {Operation} failed after {ElapsedMilliseconds:0.000} ms.",
            operation, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        return LocalStateFailure.Unavailable<T>();
    }

    private static AnalysisRunDetail ToDetail(AnalysisRunEntity run)
    {
        AnalysisTerminalSummary? terminal = run.State switch
        {
            AnalysisRunState.Completed => new AnalysisTerminalSummary(
                AnalysisTerminalKind.Completed,
                run.TerminalAtUnixMilliseconds!.Value,
                null,
                null),
            AnalysisRunState.CompletedWithLimitations => new AnalysisTerminalSummary(
                AnalysisTerminalKind.CompletedWithLimitations,
                run.TerminalAtUnixMilliseconds!.Value,
                run.TerminalLimitationCount,
                null),
            AnalysisRunState.Cancelled => new AnalysisTerminalSummary(
                AnalysisTerminalKind.Cancelled,
                run.TerminalAtUnixMilliseconds!.Value,
                null,
                null),
            AnalysisRunState.Failed => new AnalysisTerminalSummary(
                AnalysisTerminalKind.Failed,
                run.TerminalAtUnixMilliseconds!.Value,
                null,
                run.TerminalFailureCode),
            _ => null,
        };

        var excludedUncommittedCounts = run.ExcludedUncommittedTotal is not null
            ? new ExcludedUncommittedCounts(
                run.ExcludedUncommittedTotal.Value,
                run.ExcludedStagedCount!.Value,
                run.ExcludedUnstagedCount!.Value,
                run.ExcludedUntrackedCount!.Value,
                run.ExcludedConflictedCount!.Value)
            : null;

        return new AnalysisRunDetail(
            run.RunId,
            run.State,
            new AnalysisRepositoryIdentity(run.RepositoryId, run.RepositoryDisplayName, run.CanonicalRepositoryPath,
                run.CanonicalRepositoryPathKey, run.HeadRevision),
            new AnalysisComparisonIdentity(run.Target, run.TargetRevision, run.FreshnessToken),
            run.ChangeContext,
            run.RequestedAtUnixMilliseconds,
            run.CaptureStartedAtUnixMilliseconds,
            run.CapturedAtUnixMilliseconds,
            run.SnapshotId,
            run.ManifestHash,
            run.CapturedChangedFileCount,
            excludedUncommittedCounts,
            run.CorrespondenceCandidateCount,
            run.DisclosedEvidenceNodeCount,
            run.ValidationRemovalCount,
            run.CancellationRequestedAtUnixMilliseconds is not null,
            terminal,
            run.InterruptedAtUnixMilliseconds,
            run.InterruptionReason);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqliteException &&
        sqliteException.Message.Contains(UniqueConstraintSqliteErrorMessageFragment, StringComparison.Ordinal);
}
