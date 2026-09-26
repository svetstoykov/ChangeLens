using ChangeLens.Core.FindingValidation.Models;
using ChangeLens.Core.Review.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.FindingValidation.Interfaces;

/// <summary>
///     Defines mechanical validation for reviewer findings against the disclosed binder evidence.
/// </summary>
public interface IFindingValidationService
{
    /// <summary>
    ///     Removes findings that do not satisfy the publication requirements for evidence and anchors.
    /// </summary>
    /// <param name="draft">The parsed reviewer draft. Cannot be <see langword="null" />.</param>
    /// <param name="binder">The binder that disclosed the available evidence. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while validating findings.</param>
    /// <returns>The findings that passed validation and the ordered removal records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="draft" /> or <paramref name="binder" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled <paramref name="cancellationToken" />.</exception>
    FindingValidationOutcome Validate(ReviewerDraft draft, EvidenceBinderModel binder, CancellationToken cancellationToken);
}
