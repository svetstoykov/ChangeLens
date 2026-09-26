using ChangeLens.Core.EvidenceBinder.Services;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Review.Interfaces;
using ChangeLens.Core.Review.Models;
using Microsoft.Extensions.Logging;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Core.Review.Services;

/// <summary>
///     Calls the completion port once and parses the returned reviewer draft.
/// </summary>
public sealed class ReviewerService : IReviewerService
{
    private readonly IModelCompletionClient _completionClient;
    private readonly ReviewerOptions _options;
    private readonly ILogger<ReviewerService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReviewerService" /> class.
    /// </summary>
    /// <param name="completionClient">The provider-neutral completion client. Cannot be <see langword="null" />.</param>
    /// <param name="options">The reviewer call limits. Cannot be <see langword="null" />.</param>
    /// <param name="logger">The logger for completion outcomes. Cannot be <see langword="null" />.</param>
    public ReviewerService(IModelCompletionClient completionClient, ReviewerOptions options, ILogger<ReviewerService> logger)
    {
        ArgumentNullException.ThrowIfNull(completionClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this._completionClient = completionClient;
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ReviewerOutcome>> ReviewAsync(EvidenceBinderModel binder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binder);
        cancellationToken.ThrowIfCancellationRequested();

        var systemMessage = ReviewerSystemMessage.Render(binder.Contract);
        var userMessage = EvidenceBinderJson.SerializePayload(binder);
        var request = new ModelCompletionRequest(systemMessage, userMessage, this._options.MaximumOutputTokens, this._options.ReasoningEffort);
        var completionResult = await this._completionClient.CompleteJsonAsync(request, cancellationToken);
        if (completionResult.IsFailure)
        {
            return Result.ErrorFromResult<ReviewerOutcome>(completionResult);
        }

        var completion = completionResult.Data
            ?? throw new InvalidOperationException("A successful model completion result must contain a completion.");
        var inputCharacters = checked(systemMessage.Length + userMessage.Length);
        var outputCharacters = completion.Text.Length;
        ReviewerDraft draft;
        string? parseFailureReason;
        if (outputCharacters > this._options.MaximumOutputCharacters)
        {
            draft = ReviewerDraftJson.EmptyDraft();
            parseFailureReason = "The completion exceeds the configured output-character limit.";
        }
        else if (!ReviewerDraftJson.TryParse(completion.Text, out draft, out parseFailureReason))
        {
            parseFailureReason ??= "The completion could not be parsed as a reviewer draft.";
        }

        var diagnostics = new ReviewerDiagnostics(
            completion.Model, completion.LatencyMilliseconds, inputCharacters, outputCharacters, completion.InputTokens, completion.OutputTokens,
            completion.CachedInputTokens, completion.ReasoningTokens, parseFailureReason);
        this.LogOutcome(completion, diagnostics);
        return Result.Success(new ReviewerOutcome(draft, diagnostics));
    }

    private void LogOutcome(ModelCompletionModel completion, ReviewerDiagnostics diagnostics)
    {
        this._logger.LogInformation(
            "Reviewer completion {Outcome} from model {Model} in {LatencyMilliseconds:0.000} ms with {InputCharacters} input characters, "
            + "{OutputCharacters} output characters, input tokens {InputTokens}, output tokens {OutputTokens}, cached input tokens "
            + "{CachedInputTokens}, and reasoning tokens {ReasoningTokens}.",
            diagnostics.ParseFailureReason is null ? "parsed" : "parseFailed", completion.Model, completion.LatencyMilliseconds,
            diagnostics.InputCharacters, diagnostics.OutputCharacters, completion.InputTokens, completion.OutputTokens, completion.CachedInputTokens,
            completion.ReasoningTokens);

        if (diagnostics.ParseFailureReason is not null)
        {
            this._logger.LogWarning(
                "Reviewer completion was recorded as a parse failure: {Reason}; output characters {OutputCharacters}.",
                diagnostics.ParseFailureReason, diagnostics.OutputCharacters);
        }
    }
}
