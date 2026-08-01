namespace ChangeLens.Engine.AnalysisRuns.Interfaces;

/// <summary>
///     Defines execution of the deterministic analysis pipeline for one claimed run.
/// </summary>
internal interface IAnalysisPipeline
{
    /// <summary>
    ///     Asynchronously runs every planned step for one claimed run to a terminal outcome.
    /// </summary>
    /// <param name="runId">The claimed run identifier.</param>
    /// <param name="userCancellationToken">The token that is canceled when the user requests cancellation.</param>
    /// <param name="shutdownToken">The token that is canceled when the host is shutting down.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RunAsync(Guid runId, CancellationToken userCancellationToken, CancellationToken shutdownToken);
}
