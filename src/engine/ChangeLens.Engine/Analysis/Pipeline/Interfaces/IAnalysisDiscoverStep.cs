using ChangeLens.Engine.Analysis.Pipeline.Models;

namespace ChangeLens.Engine.Analysis.Pipeline.Interfaces;

/// <summary>
///     Defines execution of the analysis pipeline discover step for one captured run.
/// </summary>
internal interface IAnalysisDiscoverStep
{
    /// <summary>
    ///     Asynchronously analyzes, ranks, graphs, discloses, and binds frozen evidence for <paramref name="runId" />.
    /// </summary>
    /// <param name="runId">The taken run identifier.</param>
    /// <param name="captureOutput">The completed capture output. Cannot be <see langword="null" />.</param>
    /// <param name="userCancellationToken">The token that is canceled when the user requests cancellation.</param>
    /// <returns>
    ///     A task whose result contains the discover step outcome and, on completion, the <see cref="DiscoverStepOutput" />.
    /// </returns>
    Task<AnalysisStepResult<DiscoverStepOutput>> ExecuteAsync(Guid runId, CaptureStepOutput captureOutput, CancellationToken userCancellationToken);
}
