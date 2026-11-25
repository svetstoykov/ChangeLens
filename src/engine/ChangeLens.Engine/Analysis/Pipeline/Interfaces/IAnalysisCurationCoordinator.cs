using ChangeLens.Engine.Analysis.Pipeline.Models;
using EvidenceBinderModel = ChangeLens.Core.Analysis.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Engine.Analysis.Pipeline.Interfaces;

/// <summary>
///     Defines concurrent curator and reviewer execution followed by mechanical draft validation for one run.
/// </summary>
internal interface IAnalysisCurationCoordinator
{
    /// <summary>
    ///     Asynchronously runs curation and optional review over <paramref name="binder" /> and validates the resulting
    ///     drafts.
    /// </summary>
    /// <param name="runId">The taken run identifier.</param>
    /// <param name="binder">The assembled evidence binder. Cannot be <see langword="null" />.</param>
    /// <param name="userCancellationToken">The token that is canceled when the user requests cancellation.</param>
    /// <returns>
    ///     A task whose result contains the step outcome and, on completion, the <see cref="CurationStepOutput" />.
    /// </returns>
    Task<AnalysisStepResult<CurationStepOutput>> ExecuteAsync(Guid runId, EvidenceBinderModel binder, CancellationToken userCancellationToken);
}
