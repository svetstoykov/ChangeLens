using ChangeLens.Engine.Analysis.Pipeline.Models;

namespace ChangeLens.Engine.Analysis.Pipeline.Interfaces;

/// <summary>
///     Defines execution of the analysis pipeline capture step for one run.
/// </summary>
internal interface IAnalysisCaptureStep
{
    /// <summary>Asynchronously captures and durably commits the frozen snapshot manifest for <paramref name="runId" />.</summary>
    /// <param name="runId">The taken run identifier.</param>
    /// <param name="userCancellationToken">The token that is canceled when the user requests cancellation.</param>
    /// <returns>
    ///     A task whose result contains the capture step outcome and, on completion, the <see cref="CaptureStepOutput" />.
    /// </returns>
    Task<AnalysisStepResult<CaptureStepOutput>> ExecuteAsync(Guid runId, CancellationToken userCancellationToken);
}
