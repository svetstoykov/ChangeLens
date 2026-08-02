using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Wraps the production analysis run store and notifies one test the instant a capture commit succeeds.
/// </summary>
internal sealed class CaptureCommitObservingAnalysisRunStore(IAnalysisRunStore inner, Func<Guid, Task> onCaptureCommitted)
    : IAnalysisRunStore
{
    /// <inheritdoc />
    public Task<Result<AnalysisStartOutcome>> CreateOrReturnActiveAsync(AnalysisRunAcceptance acceptance, CancellationToken cancellationToken) =>
        inner.CreateOrReturnActiveAsync(acceptance, cancellationToken);

    /// <inheritdoc />
    public Task<Result<AnalysisRunDetail>> GetDetailAsync(Guid runId, CancellationToken cancellationToken) =>
        inner.GetDetailAsync(runId, cancellationToken);

    /// <inheritdoc />
    public Task<Result<AnalysisRunDetail?>> GetActiveByRepositoryAsync(string canonicalRepositoryPathKey, CancellationToken cancellationToken) =>
        inner.GetActiveByRepositoryAsync(canonicalRepositoryPathKey, cancellationToken);

    /// <inheritdoc />
    public Task<Result<Guid?>> TakeNextPendingAsync(CancellationToken cancellationToken) =>
        inner.TakeNextPendingAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Result> EstablishStepPlanAsync(Guid runId, IReadOnlyList<AnalysisRunStepPlanEntry> plan, CancellationToken cancellationToken) =>
        inner.EstablishStepPlanAsync(runId, plan, cancellationToken);

    /// <inheritdoc />
    public Task<Result<AnalysisRunState>> TransitionStageAsync(
        Guid runId,
        AnalysisRunState expectedCurrentState,
        AnalysisRunState nextState,
        long atUnixMilliseconds,
        CancellationToken cancellationToken) =>
        inner.TransitionStageAsync(runId, expectedCurrentState, nextState, atUnixMilliseconds, cancellationToken);

    /// <inheritdoc />
    public Task<Result> BeginStepAsync(Guid runId, string stepId, long atUnixMilliseconds, CancellationToken cancellationToken) =>
        inner.BeginStepAsync(runId, stepId, atUnixMilliseconds, cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> FinishStepAsync(
        Guid runId,
        AnalysisRunStepOutcome outcome,
        long atUnixMilliseconds,
        CancellationToken cancellationToken) =>
        inner.FinishStepAsync(runId, outcome, atUnixMilliseconds, cancellationToken);

    /// <inheritdoc />
    public Task<Result<AnalysisRunDetail>> RequestCancellationAsync(Guid runId, long atUnixMilliseconds, CancellationToken cancellationToken) =>
        inner.RequestCancellationAsync(runId, atUnixMilliseconds, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<bool>> CommitCaptureAsync(
        Guid runId,
        SnapshotCapture capture,
        long capturedAtUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        var result = await inner.CommitCaptureAsync(runId, capture, capturedAtUnixMilliseconds, cancellationToken);
        if (result is { IsSuccess: true, Data: true })
        {
            await onCaptureCommitted(runId);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<Result<bool>> CommitTerminalAsync(Guid runId, AnalysisTerminalSummary terminal, CancellationToken cancellationToken) =>
        inner.CommitTerminalAsync(runId, terminal, cancellationToken);

    /// <inheritdoc />
    public Task<Result<int>> FinalizeCancelledPendingRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken) =>
        inner.FinalizeCancelledPendingRunsAsync(atUnixMilliseconds, cancellationToken);

    /// <inheritdoc />
    public Task<Result<int>> InterruptActiveRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken) =>
        inner.InterruptActiveRunsAsync(atUnixMilliseconds, cancellationToken);
}
