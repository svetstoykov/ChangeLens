using System.Diagnostics;
using ChangeLens.Core.Analysis.ClaimChecking.Constants;
using ChangeLens.Core.Analysis.ClaimChecking.Helpers;
using ChangeLens.Core.Analysis.ClaimChecking.Interfaces;
using ChangeLens.Core.Analysis.ClaimChecking.Models;
using ChangeLens.Core.Analysis.DraftValidation.Models;
using ChangeLens.Core.Results.Models;
using Microsoft.Extensions.Logging;
using EvidenceBinderModel = ChangeLens.Core.Analysis.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.Analysis.ClaimChecking.Services;

/// <summary>
///     Builds quote-only claims, calls the checker port, and applies its verdicts deterministically.
/// </summary>
/// <remarks>
///     The Engine registers this service as scoped. It serves one analysis request and does not need to be thread-safe.
/// </remarks>
public sealed class ClaimCheckingService : IClaimCheckingService
{
    private readonly ILogger<ClaimCheckingService> _logger;
    private readonly IClaimChecker? _checker;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClaimCheckingService" /> class.
    /// </summary>
    /// <param name="logger">The logger for claim-checking outcomes. Cannot be <see langword="null" />.</param>
    /// <param name="checker">The optional checker port to call.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger" /> is <see langword="null" />.</exception>
    public ClaimCheckingService(ILogger<ClaimCheckingService> logger, IClaimChecker? checker = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this._logger = logger;
        this._checker = checker;
    }

    /// <inheritdoc />
    public bool IsAvailable => this._checker is not null;

    /// <inheritdoc />
    public async Task<Result<ClaimCheckingOutcome>> CheckAsync(
        DraftValidationOutcome validation,
        EvidenceBinderModel binder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(binder);
        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = Stopwatch.GetTimestamp();
        if (this._checker is null)
        {
            this._logger.LogWarning(
                "Claim checking for run {RunId} is unavailable because no checker adapter is registered ({ErrorCode}).",
                binder.Comparison.RunId, ClaimCheckingErrorCode.Unavailable);
            return Result.Fail<ClaimCheckingOutcome>(OperationError.ExternalDependencyFailure(
                "Claim checking is unavailable because no checker adapter is registered.",
                ClaimCheckingErrorCode.Unavailable));
        }

        var model = MentalModelFactory.FromDraft(validation.Draft);
        var claims = CheckerClaimBuilder.Build(model, binder);
        var replyResult = await this._checker.CheckAsync(claims, cancellationToken);
        if (replyResult.IsFailure)
        {
            return Result.ErrorFromResult<ClaimCheckingOutcome>(replyResult);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var reply = replyResult.Data
            ?? throw new InvalidOperationException("Successful claim checker result did not contain a reply.");
        var outcome = ClaimVerdictApplier.Apply(model, claims, reply, binder);
        this._logger.LogInformation(
            "Claim checking completed for run {RunId} with {SubmittedCount} submitted, {RetainedCount} retained, " +
            "{RemovedCount} removed, {CorrectedCount} corrected, {UncheckedCount} unchecked, and parse failed {ParseFailed} " +
            "in {ElapsedMilliseconds:0.000} ms.",
            binder.Comparison.RunId, outcome.Summary.SubmittedCount, outcome.Summary.RetainedCount,
            outcome.Summary.RemovedCount, outcome.Summary.CorrectedCount, outcome.Summary.UncheckedCount,
            outcome.Summary.ParseFailed, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        return outcome;
    }
}
