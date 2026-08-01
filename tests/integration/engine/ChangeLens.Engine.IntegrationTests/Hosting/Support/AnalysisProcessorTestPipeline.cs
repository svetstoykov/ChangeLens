using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Engine.AnalysisRuns.Interfaces;

namespace ChangeLens.Engine.IntegrationTests.Hosting.Support;

/// <summary>
///     Runs caller-supplied controlled pipeline behavior for analysis processor hosting tests.
/// </summary>
/// <param name="run">The controlled behavior for one claimed run. Cannot be <see langword="null" />.</param>
/// <param name="completeSuccessfulRuns">Whether a successful controlled run commits a completed terminal state.</param>
/// <param name="store">The durable run store. Cannot be <see langword="null" />.</param>
/// <param name="timeProvider">The clock used for controlled terminal timestamps. Cannot be <see langword="null" />.</param>
internal sealed class AnalysisProcessorTestPipeline(
    Func<Guid, CancellationToken, CancellationToken, Task> run,
    bool completeSuccessfulRuns,
    IAnalysisRunStore store,
    TimeProvider timeProvider) : IAnalysisPipeline
{
    /// <inheritdoc />
    public async Task RunAsync(
        Guid runId,
        AnalysisCheckSelection checks,
        CancellationToken userCancellationToken,
        CancellationToken shutdownToken)
    {
        await run(runId, userCancellationToken, shutdownToken);

        if (completeSuccessfulRuns)
        {
            var terminal = new AnalysisTerminalSummary(
                AnalysisTerminalKind.Completed,
                timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                null,
                null);
            await store.CommitTerminalAsync(runId, terminal, CancellationToken.None);
        }
    }
}
