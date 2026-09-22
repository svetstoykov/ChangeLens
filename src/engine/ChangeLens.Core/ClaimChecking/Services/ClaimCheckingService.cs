using ChangeLens.Core.ClaimChecking.Helpers;
using ChangeLens.Core.ClaimChecking.Interfaces;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.MentalModels.Helpers;
using ChangeLens.Core.Results.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.ClaimChecking.Services;

/// <summary>
///     Builds quote-only claims, calls the checker port, and applies its verdicts deterministically.
/// </summary>
public sealed class ClaimCheckingService : IClaimCheckingService
{
    private readonly IClaimChecker? _checker;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClaimCheckingService" /> class.
    /// </summary>
    /// <param name="checker">The optional checker port to call.</param>
    public ClaimCheckingService(IClaimChecker? checker = null)
    {
        this._checker = checker;
    }

    /// <inheritdoc />
    public async Task<Result<ClaimCheckingOutcome>> CheckAsync(
        DraftValidationOutcome validation,
        EvidenceBinderModel binder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(binder);
        cancellationToken.ThrowIfCancellationRequested();
        if (this._checker is null)
        {
            return Result.Fail<ClaimCheckingOutcome>(OperationError.ExternalDependencyFailure(
                "Claim checking is unavailable because no checker adapter is registered."));
        }

        var model = MentalModelFactory.FromDraft(validation.Draft);
        var claims = CheckerClaimBuilder.Build(model, binder);
        var replyResult = await this._checker.CheckAsync(claims, cancellationToken);
        if (replyResult.IsFailure)
        {
            return Result.ErrorFromResult<ClaimCheckingOutcome>(replyResult);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ClaimVerdictApplier.Apply(model, claims, replyResult.Data!, binder);
    }
}
