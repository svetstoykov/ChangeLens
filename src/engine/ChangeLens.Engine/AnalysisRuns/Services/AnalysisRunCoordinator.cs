using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Interfaces;

namespace ChangeLens.Engine.AnalysisRuns.Services;

/// <summary>
///     Orchestrates the four analysis-run use cases over the durable store and comparison-freshness capability.
/// </summary>
internal sealed class AnalysisRunCoordinator(
    IAnalysisRunStore store,
    IGitComparisonFreshnessChecker freshnessChecker,
    IRepositoryPathResolver pathResolver,
    ICanonicalRepositoryPathKeyProvider pathKeyProvider,
    IAnalysisProcessorControl processorControl,
    TimeProvider timeProvider) : IAnalysisRunCoordinator
{
    /// <inheritdoc />
    public async Task<Result<AnalysisStartOutcome>> StartAsync(
        string? path,
        string? target,
        string? freshnessToken,
        string? changeContext,
        CancellationToken cancellationToken)
    {
        var freshnessResult = await freshnessChecker.CheckAsync(path, target, freshnessToken, cancellationToken);
        if (freshnessResult.IsFailure)
        {
            return Result.ErrorFromResult<AnalysisStartOutcome>(freshnessResult);
        }

        var freshnessCheck = freshnessResult.Data!;
        if (freshnessCheck.State == ComparisonFreshnessState.Stale)
        {
            return new AnalysisStartOutcome(AnalysisStartOutcomeKind.RejectedStale, null, null, null);
        }

        var repository = freshnessCheck.Repository!;
        var headRevision = repository.Head switch
        {
            BranchRepositoryHead branch => branch.Revision,
            DetachedRepositoryHead detached => detached.Revision,
            _ => throw new ArgumentOutOfRangeException(nameof(repository), "Unapproved repository head shape."),
        };
        var canonicalPathKey = pathKeyProvider.CreateKey(repository.CanonicalPath);
        var requestedAt = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var acceptance = new AnalysisRunAcceptance(
            canonicalPathKey,
            repository.Name,
            repository.CanonicalPath,
            headRevision,
            target!,
            freshnessCheck.TargetRevision!,
            freshnessToken!,
            changeContext,
            requestedAt);

        var outcome = await store.CreateOrReturnActiveAsync(acceptance, cancellationToken);
        if (outcome is { IsSuccess: true, Data.Kind: AnalysisStartOutcomeKind.Accepted })
        {
            processorControl.SignalPendingWork();
        }

        return outcome;
    }

    /// <inheritdoc />
    public async Task<Result<AnalysisRunDetail?>> GetActiveAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationError.NotFound(
                "The repository path could not be resolved to an available repository.",
                AnalysisErrorCode.RepositoryUnavailable);
        }

        var resolution = await pathResolver.ResolveAsync(path, cancellationToken);
        if (resolution.IsFailure)
        {
            return OperationError.NotFound(
                "The repository path could not be resolved to an available repository.",
                AnalysisErrorCode.RepositoryUnavailable);
        }

        var canonicalPathKey = pathKeyProvider.CreateKey(resolution.Data!);
        return await store.GetActiveByRepositoryAsync(canonicalPathKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<AnalysisRunDetail>> PollRunAsync(Guid runId, CancellationToken cancellationToken) =>
        store.GetDetailAsync(runId, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> CancelAsync(Guid runId, CancellationToken cancellationToken)
    {
        var atUnixMilliseconds = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var detailResult = await store.RequestCancellationAsync(runId, atUnixMilliseconds, cancellationToken);
        if (detailResult.IsFailure)
        {
            return Result.ErrorFromResult(detailResult);
        }

        processorControl.RequestRunCancellation(runId);
        processorControl.SignalPendingWork();
        return Result.Success();
    }
}
