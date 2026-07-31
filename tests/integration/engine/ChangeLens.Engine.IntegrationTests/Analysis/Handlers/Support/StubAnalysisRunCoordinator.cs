using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Interfaces;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Handlers.Support;

/// <summary>
///     Provides caller-selected analysis coordinator outcomes for action-handler integration tests.
/// </summary>
internal sealed class StubAnalysisRunCoordinator(
    Func<string?, string?, string?, AnalysisCheckSelection, string?, CancellationToken, Task<Result<AnalysisStartOutcome>>>? start = null,
    Func<string?, CancellationToken, Task<Result<AnalysisRunDetail?>>>? getActive = null,
    Func<Guid, CancellationToken, Task<Result<AnalysisRunDetail>>>? pollRun = null,
    Func<Guid, CancellationToken, Task<Result>>? cancel = null) : IAnalysisRunCoordinator
{
    internal bool PollCalled { get; private set; }

    public Task<Result<AnalysisStartOutcome>> StartAsync(
        string? path,
        string? target,
        string? freshnessToken,
        AnalysisCheckSelection checks,
        string? changeContext,
        CancellationToken cancellationToken) =>
        start?.Invoke(path, target, freshnessToken, checks, changeContext, cancellationToken)
        ?? throw new NotSupportedException("The start operation was not configured.");

    public Task<Result<AnalysisRunDetail?>> GetActiveAsync(string? path, CancellationToken cancellationToken) =>
        getActive?.Invoke(path, cancellationToken)
        ?? throw new NotSupportedException("The active-run operation was not configured.");

    public Task<Result<AnalysisRunDetail>> PollRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        this.PollCalled = true;
        return pollRun?.Invoke(runId, cancellationToken)
            ?? throw new NotSupportedException("The poll operation was not configured.");
    }

    public Task<Result> CancelAsync(Guid runId, CancellationToken cancellationToken) =>
        cancel?.Invoke(runId, cancellationToken)
        ?? throw new NotSupportedException("The cancel operation was not configured.");
}
