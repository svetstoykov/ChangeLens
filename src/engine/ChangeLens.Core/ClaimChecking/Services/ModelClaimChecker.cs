using ChangeLens.Core.ClaimChecking.Interfaces;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using Microsoft.Extensions.Logging;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Core.ClaimChecking.Services;

/// <summary>
///     Checks claims through one quote-only JSON completion on the provider-neutral completion port.
/// </summary>
/// <remarks>
///     Whole claims are fitted, in order, to the configured claim count and payload-character bound; the first claim
///     that does not fit ends the payload, and it and every later claim receive no verdict. A reply without a readable
///     verdict is retried with a firmer instruction and the same payload until the configured attempts are spent.
/// </remarks>
public sealed class ModelClaimChecker : IClaimChecker
{
    private const int DivergenceMinimumOutputTokens = 500;
    private const double DivergenceCharactersPerToken = 0.1;

    private readonly IModelCompletionClient _completionClient;
    private readonly ClaimCheckingOptions _options;
    private readonly ILogger<ModelClaimChecker> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelClaimChecker" /> class.
    /// </summary>
    /// <param name="completionClient">The provider-neutral completion client. Cannot be <see langword="null" />.</param>
    /// <param name="options">The checker call limits. Cannot be <see langword="null" />.</param>
    /// <param name="logger">The logger for checker call outcomes. Cannot be <see langword="null" />.</param>
    public ModelClaimChecker(
        IModelCompletionClient completionClient,
        ClaimCheckingOptions options,
        ILogger<ModelClaimChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(completionClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this._completionClient = completionClient;
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ClaimCheckerReply>> CheckAsync(IReadOnlyList<CheckerClaim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claims);
        cancellationToken.ThrowIfCancellationRequested();

        var submitted = this.Fit(claims);
        this._logger.LogInformation(
            "Claim checker submits {SubmittedCount} of {ClaimCount} claims; {BeyondBoundCount} claims past the bound receive no verdict.",
            submitted.Count, claims.Count, claims.Count - submitted.Count);
        if (submitted.Count == 0)
        {
            this._logger.LogWarning(
                "No claim fits the checker bound of {MaximumPayloadCharacters} payload characters.", this._options.MaximumPayloadCharacters);
            return Result.Success(new ClaimCheckerReply([], null));
        }

        var payload = CheckerJson.SerializePayload(submitted);
        var submittedClaimIds = submitted.Select(claim => claim.ClaimId).ToHashSet(StringComparer.Ordinal);
        string? failureReason = null;
        for (var attempt = 1; attempt <= this._options.Attempts; attempt++)
        {
            var systemMessage = attempt == 1 ? CheckerSystemMessage.Render() : CheckerSystemMessage.RenderRetry();
            var request = new ModelCompletionRequest(systemMessage, payload, this._options.MaximumOutputTokens, this._options.ReasoningEffort);
            var completionResult = await this._completionClient.CompleteJsonAsync(request, cancellationToken);
            if (completionResult.IsFailure)
            {
                return Result.ErrorFromResult<ClaimCheckerReply>(completionResult);
            }

            var completion = completionResult.Data
                ?? throw new InvalidOperationException("A successful model completion result must contain a completion.");
            this.LogAttempt(attempt, completion, checked(systemMessage.Length + payload.Length));
            if (CheckerJson.TryParse(completion.Text, submittedClaimIds, out var verdicts, out var unreadableCount, out failureReason))
            {
                if (unreadableCount > 0)
                {
                    this._logger.LogWarning(
                        "Claim checker attempt {Attempt} skipped {UnreadableCount} unreadable verdict entries; their claims stay unanswered.",
                        attempt, unreadableCount);
                }

                return Result.Success(new ClaimCheckerReply(verdicts, null));
            }

            this._logger.LogWarning(
                "Claim checker attempt {Attempt} of {Attempts} produced no readable verdict: {Reason}; output characters {OutputCharacters}.",
                attempt, this._options.Attempts, failureReason, completion.Text.Length);
        }

        return Result.Success(new ClaimCheckerReply([], failureReason));
    }

    private IReadOnlyList<CheckerClaim> Fit(IReadOnlyList<CheckerClaim> claims)
    {
        var selected = new List<CheckerClaim>();
        var used = CheckerJson.EmptyPayload.Length;
        foreach (var claim in claims)
        {
            if (selected.Count >= this._options.MaximumClaims)
            {
                break;
            }

            var size = CheckerJson.SerializeClaim(claim).Length + (selected.Count > 0 ? 1 : 0);
            if (used + size > this._options.MaximumPayloadCharacters)
            {
                break;
            }

            used += size;
            selected.Add(claim);
        }

        return selected;
    }

    private void LogAttempt(int attempt, ModelCompletionModel completion, int inputCharacters)
    {
        this._logger.LogInformation(
            "Claim checker attempt {Attempt} of {Attempts} completed by model {Model} in {LatencyMilliseconds:0.000} ms with {InputCharacters} "
            + "input characters, {OutputCharacters} output characters, input tokens {InputTokens}, output tokens {OutputTokens}, cached input "
            + "tokens {CachedInputTokens}, and reasoning tokens {ReasoningTokens}.",
            attempt, this._options.Attempts, completion.Model, completion.LatencyMilliseconds, inputCharacters, completion.Text.Length,
            completion.InputTokens, completion.OutputTokens, completion.CachedInputTokens, completion.ReasoningTokens);

        if (completion.OutputTokens is > DivergenceMinimumOutputTokens
            && completion.Text.Length < completion.OutputTokens.Value * DivergenceCharactersPerToken)
        {
            this._logger.LogWarning(
                "Claim checker attempt {Attempt} spent {OutputTokens} output tokens on {OutputCharacters} content characters; reasoning "
                + "likely consumed the output budget.",
                attempt, completion.OutputTokens, completion.Text.Length);
        }
    }
}
