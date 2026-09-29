using ChangeLens.Core.ClaimChecking.Interfaces;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.Results.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Engine.IntegrationTests.Publication.Support;

/// <summary>
///     Returns a canned claim-checking outcome and records whether it was called.
/// </summary>
/// <param name="outcome">The canned checking outcome returned on each call.</param>
internal sealed class RecordingClaimCheckingService(ClaimCheckingOutcome outcome) : IClaimCheckingService
{
    /// <summary>
    ///     Gets a value indicating whether <see cref="CheckAsync" /> was called.
    /// </summary>
    internal bool Called => this.CallCount > 0;

    /// <summary>
    ///     Gets the number of times <see cref="CheckAsync" /> was called.
    /// </summary>
    internal int CallCount { get; private set; }

    /// <inheritdoc />
    public Task<Result<ClaimCheckingOutcome>> CheckAsync(
        DraftValidationOutcome validation,
        EvidenceBinderModel binder,
        CancellationToken cancellationToken)
    {
        this.CallCount++;
        return Task.FromResult(Result.Success(outcome));
    }
}
