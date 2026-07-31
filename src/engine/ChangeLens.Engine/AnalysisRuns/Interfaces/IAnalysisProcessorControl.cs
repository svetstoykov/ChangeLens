namespace ChangeLens.Engine.AnalysisRuns.Interfaces;

/// <summary>
///     Coordinates the in-memory signals shared between request handling and the analysis processor loop.
/// </summary>
internal interface IAnalysisProcessorControl
{
    /// <summary>Signals that pending analysis work may be available.</summary>
    void SignalPendingWork();

    /// <summary>Asynchronously waits for a pending-work signal or cancellation.</summary>
    /// <param name="cancellationToken">The token that cancels the wait.</param>
    /// <returns>A task that represents the asynchronous wait.</returns>
    Task WaitForPendingWorkAsync(CancellationToken cancellationToken);

    /// <summary>Claims the single in-process run slot.</summary>
    /// <param name="runId">The identifier of the run to claim.</param>
    /// <returns>The token used to cancel the claimed run.</returns>
    CancellationToken BeginRun(Guid runId);

    /// <summary>Releases the run slot for the specified run.</summary>
    /// <param name="runId">The identifier of the run that finished.</param>
    void EndRun(Guid runId);

    /// <summary>Requests cancellation of the specified run when it is executing in this process.</summary>
    /// <param name="runId">The identifier of the run to cancel.</param>
    void RequestRunCancellation(Guid runId);
}
