using ChangeLens.Core.Analysis.ClaimChecking.Models;
using ChangeLens.Core.Analysis.DraftValidation.Models;
using ChangeLens.Core.Results.Models;
using EvidenceBinderModel = ChangeLens.Core.Analysis.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.Analysis.ClaimChecking.Interfaces;

/// <summary>
///     Defines deterministic application of checker verdicts to a validated draft.
/// </summary>
public interface IClaimCheckingService
{
    /// <summary>
    ///     Gets a value indicating whether a claim-checker adapter is available.
    /// </summary>
    bool IsAvailable { get; }

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
