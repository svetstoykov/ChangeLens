using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Engine.Analysis.Pipeline.Models;

namespace ChangeLens.Engine.Analysis.Pipeline.Interfaces;

/// <summary>
///     Defines publication and protocol projection for one curated and validated analysis run.
/// </summary>
internal interface IAnalysisPublicationStep
{
    /// <summary>
    ///     Asynchronously publishes the reading model, records validation removals, and projects the poll response payload
    ///     within the transport budget.
    /// </summary>
    /// <param name="runId">The taken run identifier.</param>
    /// <param name="discovery">The discover step output. Cannot be <see langword="null" />.</param>
    /// <param name="curation">The curation step output. Cannot be <see langword="null" />.</param>
    /// <param name="userCancellationToken">The token that is canceled when the user requests cancellation.</param>
    /// <returns>
    ///     A task whose result contains the collect step outcome and, on completion, the renderable
    ///     <see cref="AnalysisReadingProjection" />.
    /// </returns>
    Task<AnalysisStepResult<AnalysisReadingProjection>> ExecuteAsync(
        Guid runId, DiscoverStepOutput discovery, CurationStepOutput curation, CancellationToken userCancellationToken);
}
