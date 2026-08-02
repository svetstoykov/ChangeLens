using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Provides caller-selected durable analysis-store outcomes for coordinator integration tests.
/// </summary>
internal sealed class StubAnalysisRunStore(
    Func<AnalysisRunAcceptance, CancellationToken, Task<Result<AnalysisStartOutcome>>>? create = null,
    Func<Guid, long, CancellationToken, Task<Result<AnalysisRunDetail>>>? requestCancellation = null) : IAnalysisRunStore
{
    public Task<Result<AnalysisStartOutcome>> CreateOrReturnActiveAsync(AnalysisRunAcceptance acceptance, CancellationToken cancellationToken) =>
        create?.Invoke(acceptance, cancellationToken)
        ?? throw new NotSupportedException("The create operation was not configured.");

    public Task<Result<AnalysisRunDetail>> GetDetailAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The detail operation was not configured.");

    public Task<Result<AnalysisRunDetail?>> GetActiveByRepositoryAsync(string canonicalRepositoryPathKey, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The active-run operation was not configured.");

    public Task<Result<Guid?>> TakeNextPendingAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The take operation was not configured.");

    public Task<Result> EstablishStepPlanAsync(Guid runId, IReadOnlyList<AnalysisRunStepPlanEntry> plan, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The plan operation was not configured.");

    public Task<Result<AnalysisRunState>> TransitionStageAsync(
        Guid runId,
        AnalysisRunState expectedCurrentState,
        AnalysisRunState nextState,
        long atUnixMilliseconds,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("The stage operation was not configured.");

    public Task<Result> BeginStepAsync(Guid runId, string stepId, long atUnixMilliseconds, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The begin-step operation was not configured.");

    public Task<Result<bool>> FinishStepAsync(
        Guid runId,
        AnalysisRunStepOutcome outcome,
        long atUnixMilliseconds,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("The finish-step operation was not configured.");

    public Task<Result<AnalysisRunDetail>> RequestCancellationAsync(
        Guid runId,
        long atUnixMilliseconds,
        CancellationToken cancellationToken) =>
        requestCancellation?.Invoke(runId, atUnixMilliseconds, cancellationToken)
        ?? throw new NotSupportedException("The cancellation operation was not configured.");

    public Task<Result<bool>> CommitCaptureAsync(
        Guid runId,
        SnapshotCapture capture,
        long capturedAtUnixMilliseconds,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("The capture-commit operation was not configured.");

    public Task<Result<bool>> CommitTerminalAsync(
        Guid runId,
        AnalysisTerminalSummary terminal,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("The terminal operation was not configured.");

    public Task<Result<int>> FinalizeCancelledPendingRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The pending-cancellation operation was not configured.");

    public Task<Result<int>> InterruptActiveRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The interruption operation was not configured.");
}
