using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;
using ChangeLens.Infrastructure.LocalState.Persistence;
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
        AnalysisRunState.Checking,
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
            return OperationError.NotFound(
                "The repository is not retained in local state.",
                AnalysisErrorCode.RepositoryUnavailable);
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
            BuildEnabled = acceptance.Checks.Build,
            TestsEnabled = acceptance.Checks.Tests,
            ProcessorSessionId = acceptance.ProcessorSessionId,
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
                .Where(run => run.CanonicalRepositoryPathKey == acceptance.CanonicalRepositoryPathKey)
                .Where(run => ActiveStates.Contains(run.State))
                .Select(run => (Guid?)run.RunId)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeRunId is null)
            {
                logger.LogError("The unique repository-reservation index rejected acceptance but no active row was found.");
                return OperationError.InternalError(
                    "The analysis run could not be accepted because of an unexpected conflict.",
                    AnalysisFailureCode.UnexpectedFailure);
            }

            return new AnalysisStartOutcome(AnalysisStartOutcomeKind.RejectedActive, null, null, activeRunId);
        }

        logger.LogInformation("Accepted analysis run {RunId} in PendingCapture.", runId);
        return new AnalysisStartOutcome(AnalysisStartOutcomeKind.Accepted, runId, acceptance.RequestedAtUnixMilliseconds, null);
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunDetail>> GetDetailAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await context.AnalysisRuns
            .Include(entity => entity.Steps)
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.RunId == runId, cancellationToken);

        return run is null
            ? OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun)
            : ToDetail(run);
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunDetail?>> GetActiveByRepositoryAsync(
        string canonicalRepositoryPathKey,
        CancellationToken cancellationToken)
    {
        var run = await context.AnalysisRuns
            .Include(entity => entity.Steps)
            .AsNoTracking()
            .Where(entity => entity.CanonicalRepositoryPathKey == canonicalRepositoryPathKey)
            .Where(entity => ActiveStates.Contains(entity.State))
            .FirstOrDefaultAsync(cancellationToken);

        return run is null ? (AnalysisRunDetail?)null : ToDetail(run);
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunClaim?>> ClaimNextPendingAsync(Guid processorSessionId, CancellationToken cancellationToken)
    {
        var claimTimestamp = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var candidate = await context.AnalysisRuns
            .Where(run => run.State == AnalysisRunState.PendingCapture)
            .Where(run => run.ProcessorSessionId == processorSessionId)
            .Where(run => run.CancellationRequestedAtUnixMilliseconds == null)
            .OrderBy(run => run.RequestedAtUnixMilliseconds)
            .ThenBy(run => run.RunId)
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return (AnalysisRunClaim?)null;
        }

        var affected = await context.AnalysisRuns
            .Where(run => run.RunId == candidate.RunId && run.State == AnalysisRunState.PendingCapture)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.State, AnalysisRunState.Capturing)
                    .SetProperty(run => run.CaptureStartedAtUnixMilliseconds, claimTimestamp),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (affected == 0)
        {
            return (AnalysisRunClaim?)null;
        }

        logger.LogInformation("Claimed analysis run {RunId} for processor session {ProcessorSessionId}.", candidate.RunId, processorSessionId);
        return new AnalysisRunClaim(candidate.RunId, new AnalysisCheckSelection(candidate.BuildEnabled, candidate.TestsEnabled));
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
    public async Task<Result<AnalysisRunDetail>> RequestCancellationAsync(
        Guid runId,
        long atUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        await context.AnalysisRuns
            .Where(run => run.RunId == runId)
            .Where(run => ActiveStates.Contains(run.State))
            .Where(run => run.CancellationRequestedAtUnixMilliseconds == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(run => run.CancellationRequestedAtUnixMilliseconds, atUnixMilliseconds),
                cancellationToken);

        return await this.GetDetailAsync(runId, cancellationToken);
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
            .Where(run => run.RunId == runId)
            .Where(run => ActiveStates.Contains(run.State))
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
    public async Task<Result<int>> FinalizeCancelledPendingRunsAsync(
        Guid processorSessionId,
        long atUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        var affected = await context.AnalysisRuns
            .Where(run => run.ProcessorSessionId == processorSessionId)
            .Where(run => run.State == AnalysisRunState.PendingCapture)
            .Where(run => run.CancellationRequestedAtUnixMilliseconds != null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.State, AnalysisRunState.Cancelled)
                    .SetProperty(run => run.TerminalAtUnixMilliseconds, atUnixMilliseconds),
                cancellationToken);

        return affected;
    }

    /// <inheritdoc />
    public async Task<Result<int>> InterruptEarlierSessionRowsAsync(
        Guid currentProcessorSessionId,
        long atUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        var affected = await context.AnalysisRuns
            .Where(run => run.ProcessorSessionId != currentProcessorSessionId)
            .Where(run => ActiveStates.Contains(run.State))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.State, AnalysisRunState.Interrupted)
                    .SetProperty(run => run.InterruptedAtUnixMilliseconds, atUnixMilliseconds)
                    .SetProperty(run => run.InterruptionReason, AnalysisInterruptionReason.EngineStopped),
                cancellationToken);

        if (affected > 0)
        {
            logger.LogInformation("Startup recovery interrupted {InterruptedCount} run(s) from earlier processor sessions.", affected);
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

        return new AnalysisRunDetail(
            run.RunId,
            run.State,
            new AnalysisRepositoryIdentity(run.RepositoryId, run.RepositoryDisplayName, run.CanonicalRepositoryPath, run.HeadRevision),
            new AnalysisComparisonIdentity(run.Target, run.TargetRevision, run.FreshnessToken),
            new AnalysisCheckSelection(run.BuildEnabled, run.TestsEnabled),
            run.RequestedAtUnixMilliseconds,
            run.CaptureStartedAtUnixMilliseconds,
            run.CancellationRequestedAtUnixMilliseconds is not null,
            terminal,
            run.InterruptedAtUnixMilliseconds,
            run.InterruptionReason);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqliteException &&
        sqliteException.Message.Contains(UniqueConstraintSqliteErrorMessageFragment, StringComparison.Ordinal);
}
