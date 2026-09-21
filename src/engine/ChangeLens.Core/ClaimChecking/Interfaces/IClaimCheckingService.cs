using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.Results.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.ClaimChecking.Interfaces;

/// <summary>
///     Defines deterministic application of checker verdicts to a validated draft.
/// </summary>
public interface IClaimCheckingService
{
    /// <summary>
    ///     Asynchronously checks and publishes a validated draft.
    /// </summary>
    /// <param name="validation">The validated draft and its measurements.</param>
    /// <param name="binder">The binder that supplied disclosed evidence.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A task whose result contains the checked model and summary.</returns>
    Task<Result<ClaimCheckingOutcome>> CheckAsync(
        DraftValidationOutcome validation,
        EvidenceBinderModel binder,
        CancellationToken cancellationToken);
}
