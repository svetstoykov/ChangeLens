using ChangeLens.Core.Analysis.Curation.Models;
using ChangeLens.Core.Analysis.DraftValidation.Models;
using EvidenceBinderModel = ChangeLens.Core.Analysis.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.Analysis.DraftValidation.Interfaces;

/// <summary>
///     Defines mechanical validation for a curator mental-model draft.
/// </summary>
public interface IDraftValidationService
{
    /// <summary>
    ///     Removes draft items that cannot be bound to the supplied evidence binder.
    /// </summary>
    /// <param name="draft">The parsed curator draft. Cannot be <see langword="null" />.</param>
    /// <param name="binder">The binder that disclosed the available evidence. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">The token to observe while validating the draft.</param>
    /// <returns>The validated draft and the mechanical validation diagnostics.</returns>
    DraftValidationOutcome Validate(MentalModelDraft draft, EvidenceBinderModel binder, CancellationToken cancellationToken);
}
