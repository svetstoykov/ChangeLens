using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.Snapshots.Persistence.Entities;
using Microsoft.Data.Sqlite;
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

    /// <inheritdoc />
    public async Task<Result<AnalysisStartOutcome>> CreateOrReturnActiveAsync(
        AnalysisRunAcceptance acceptance,
        CancellationToken cancellationToken)
    {
        var repositoryId = await context.Repositories
            .Where(repository => repository.CanonicalPathKey == acceptance.CanonicalRepositoryPathKey)
            .Select(repository => (Guid?)repository.RepositoryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (repositoryId is null)
        {
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
                logger.LogError("The unique repository-reservation index rejected acceptance but no active row was found.");
                return OperationError.InternalError(
                    "The analysis run could not be accepted because of an unexpected conflict.",
                    AnalysisFailureCode.UnexpectedFailure);
            }

            logger.LogInformation(
                "Rejected analysis run acceptance for repository {RepositoryDisplayName} because run {ActiveRunId} is already active.",
                acceptance.RepositoryDisplayName, activeRunId);
            return new AnalysisStartOutcome(AnalysisStartOutcomeKind.RejectedActive, null, null, activeRunId);
        }

        logger.LogInformation("Accepted analysis run {RunId} in PendingCapture.", runId);
        return new AnalysisStartOutcome(AnalysisStartOutcomeKind.Accepted, runId, acceptance.RequestedAtUnixMilliseconds, null);
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunDetail>> GetDetailAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await context.AnalysisRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.RunId == runId, cancellationToken);

        return run is null
            ? OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun)
            : ToDetail(run);
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunDetail?>> GetActiveByRepositoryAsync(string canonicalRepositoryPathKey, CancellationToken cancellationToken)
    {
        var run = await context.AnalysisRuns
            .AsNoTracking()
            .Where(entity => entity.CanonicalRepositoryPathKey == canonicalRepositoryPathKey && ActiveStates.Contains(entity.State))
            .FirstOrDefaultAsync(cancellationToken);

        return run is null ? null : ToDetail(run);
    }

    /// <inheritdoc />
    public async Task<Result<Guid?>> TakeNextPendingAsync(CancellationToken cancellationToken)
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
            return (Guid?)null;
        }

        var affected = await context.AnalysisRuns
            .Where(run => run.RunId == candidate.RunId && run.State == AnalysisRunState.PendingCapture)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.State, AnalysisRunState.Capturing)
                    .SetProperty(run => run.CaptureStartedAtUnixMilliseconds, takeTimestamp),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (affected == 0)
        {
            return (Guid?)null;
        }

        logger.LogInformation("Took analysis run {RunId}.", candidate.RunId);
        return candidate.RunId;
    }

    /// <inheritdoc />
    public async Task<Result> EstablishStepPlanAsync(
        Guid runId,
        IReadOnlyList<AnalysisRunStepPlanEntry> plan,
        CancellationToken cancellationToken)
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
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunState>> TransitionStageAsync(
        Guid runId,
        AnalysisRunState expectedCurrentState,
        AnalysisRunState nextState,
        long atUnixMilliseconds,
        CancellationToken cancellationToken)
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

        return currentState is null
            ? OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun)
            : (affected == 1 ? nextState : currentState.Value);
    }

    /// <inheritdoc />
    public async Task<Result> BeginStepAsync(Guid runId, string stepId, long atUnixMilliseconds, CancellationToken cancellationToken)
    {
        await context.AnalysisRunSteps
            .Where(step => step.RunId == runId && step.StepId == stepId && step.State == AnalysisRunStepState.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(step => step.State, AnalysisRunStepState.Running)
                    .SetProperty(step => step.StartedAtUnixMilliseconds, atUnixMilliseconds),
                cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<bool>> FinishStepAsync(
        Guid runId,
        AnalysisRunStepOutcome outcome,
        long atUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        var affected = await context.AnalysisRunSteps
            .Where(step => step.RunId == runId && step.StepId == outcome.StepId && step.State == AnalysisRunStepState.Running)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(step => step.State, outcome.State)
                    .SetProperty(step => step.Code, outcome.Code)
                    .SetProperty(step => step.FinishedAtUnixMilliseconds, atUnixMilliseconds),
                cancellationToken);
        return affected == 1;
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunDetail>> RequestCancellationAsync(Guid runId, long atUnixMilliseconds, CancellationToken cancellationToken)
    {
        await context.AnalysisRuns
            .Where(run => run.RunId == runId && ActiveStates.Contains(run.State))
            .Where(run => run.CancellationRequestedAtUnixMilliseconds == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(run => run.CancellationRequestedAtUnixMilliseconds, atUnixMilliseconds),
                cancellationToken);

        return await this.GetDetailAsync(runId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CommitCaptureAsync(
        Guid runId,
        SnapshotCapture capture,
        long capturedAtUnixMilliseconds,
        CancellationToken cancellationToken)
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
            return OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun);
        }

        var manifest = capture.Manifest;
        if (!StringComparer.Ordinal.Equals(analysis.CanonicalRepositoryPathKey, manifest.CanonicalRepositoryPathKey) ||
            !StringComparer.Ordinal.Equals(analysis.Target, manifest.TargetReference) ||
            !StringComparer.Ordinal.Equals(analysis.TargetRevision, manifest.TargetRevision) ||
            !StringComparer.Ordinal.Equals(analysis.HeadRevision, manifest.HeadRevision))
        {
            logger.LogError("Analysis run {RunId} rejected a capture whose manifest identity does not match its accepted run.", runId);
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
            logger.LogInformation("Analysis run {RunId} did not commit its capture because durable state already moved on.", runId);
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

        logger.LogInformation("Analysis run {RunId} committed snapshot {SnapshotId} with {EntryCount} manifest entries.",
            runId, manifest.SnapshotId, manifest.Entries.Count);

        return true;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CommitTerminalAsync(Guid runId, AnalysisTerminalSummary terminal, CancellationToken cancellationToken)
    {
        var nextState = terminal.Kind switch
        {
            AnalysisTerminalKind.Completed => AnalysisRunState.Completed,
            AnalysisTerminalKind.CompletedWithLimitations => AnalysisRunState.CompletedWithLimitations,
            AnalysisTerminalKind.Cancelled => AnalysisRunState.Cancelled,
            AnalysisTerminalKind.Failed => AnalysisRunState.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(terminal)),
        };

        var affected = await context.AnalysisRuns
            .Where(run => run.RunId == runId && ActiveStates.Contains(run.State))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.State, nextState)
                    .SetProperty(run => run.TerminalAtUnixMilliseconds, terminal.TerminalAtUnixMilliseconds)
                    .SetProperty(run => run.TerminalLimitationCount, terminal.LimitationCount)
                    .SetProperty(run => run.TerminalFailureCode, terminal.FailureCode),
                cancellationToken);

        return affected == 1;
    }

    /// <inheritdoc />
    public async Task<Result<int>> FinalizeCancelledPendingRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken)
    {
        var affected = await context.AnalysisRuns
            .Where(run => run.State == AnalysisRunState.PendingCapture && run.CancellationRequestedAtUnixMilliseconds != null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.State, AnalysisRunState.Cancelled)
                    .SetProperty(run => run.TerminalAtUnixMilliseconds, atUnixMilliseconds),
                cancellationToken);

        return affected;
    }

    /// <inheritdoc />
    public async Task<Result<int>> InterruptActiveRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken)
    {
        var affected = await context.AnalysisRuns
            .Where(run => ActiveStates.Contains(run.State))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.State, AnalysisRunState.Interrupted)
                    .SetProperty(run => run.InterruptedAtUnixMilliseconds, atUnixMilliseconds)
                    .SetProperty(run => run.InterruptionReason, AnalysisInterruptionReason.EngineStopped),
                cancellationToken);

        if (affected > 0)
        {
            logger.LogInformation("Startup recovery interrupted {InterruptedCount} active run(s).", affected);
        }

        return affected;
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
            run.RequestedAtUnixMilliseconds,
            run.CaptureStartedAtUnixMilliseconds,
            run.CapturedAtUnixMilliseconds,
            run.SnapshotId,
            run.ManifestHash,
            run.CapturedChangedFileCount,
            excludedUncommittedCounts,
            run.CancellationRequestedAtUnixMilliseconds is not null,
            terminal,
            run.InterruptedAtUnixMilliseconds,
            run.InterruptionReason);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqliteException &&
        sqliteException.Message.Contains(UniqueConstraintSqliteErrorMessageFragment, StringComparison.Ordinal);
}
