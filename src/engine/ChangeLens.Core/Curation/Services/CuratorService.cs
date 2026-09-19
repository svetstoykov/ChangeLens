using ChangeLens.Core.Curation.Interfaces;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.EvidenceBinder.Services;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using Microsoft.Extensions.Logging;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Core.Curation.Services;

/// <summary>
///     Calls the completion port once and parses the returned curator draft.
/// </summary>
public sealed class CuratorService : ICuratorService
{
    private readonly IModelCompletionClient _completionClient;
    private readonly CuratorOptions _options;
    private readonly ILogger<CuratorService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CuratorService" /> class.
    /// </summary>
    /// <param name="completionClient">The provider-neutral completion client. Cannot be <see langword="null" />.</param>
    /// <param name="options">The curator call limits. Cannot be <see langword="null" />.</param>
    /// <param name="logger">The logger for completion outcomes. Cannot be <see langword="null" />.</param>
    public CuratorService(
        IModelCompletionClient completionClient,
        CuratorOptions options,
        ILogger<CuratorService> logger)
    {
        ArgumentNullException.ThrowIfNull(completionClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this._completionClient = completionClient;
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<CuratorOutcome>> CurateAsync(EvidenceBinderModel binder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binder);
        cancellationToken.ThrowIfCancellationRequested();

        var systemMessage = CuratorSystemMessage.Render(binder);
        var userMessage = EvidenceBinderJson.SerializePayload(binder);
        var request = new ModelCompletionRequest(
            systemMessage,
            userMessage,
            this._options.MaximumOutputTokens,
            this._options.ReasoningEffort);
        var completionResult = await this._completionClient.CompleteJsonAsync(request, cancellationToken);
        if (completionResult.IsFailure)
        {
            return Result.ErrorFromResult<CuratorOutcome>(completionResult);
        }

        var completion = completionResult.Data
            ?? throw new InvalidOperationException("A successful model completion result must contain a completion.");
        var inputCharacters = checked(systemMessage.Length + userMessage.Length);
        var outputCharacters = completion.Text.Length;
        MentalModelDraft draft;
        string? parseFailureReason;
        if (outputCharacters > this._options.MaximumOutputCharacters)
        {
            draft = CuratorDraftJson.EmptyDraft();
            parseFailureReason = "The completion exceeds the configured output-character limit.";
        }
        else if (!CuratorDraftJson.TryParse(completion.Text, out draft, out parseFailureReason))
        {
            parseFailureReason ??= "The completion could not be parsed as a curator draft.";
        }

        var diagnostics = new CuratorDiagnostics(
            completion.Model,
            completion.LatencyMilliseconds,
            inputCharacters,
            outputCharacters,
            completion.InputTokens,
            completion.OutputTokens,
            completion.CachedInputTokens,
            completion.ReasoningTokens,
            parseFailureReason,
            draft.DroppedNodeIds,
            completion.Text,
            systemMessage);
        this.LogOutcome(completion, diagnostics);
        return Result.Success(new CuratorOutcome(draft, diagnostics));
    }

    private void LogOutcome(ModelCompletionModel completion, CuratorDiagnostics diagnostics)
    {
        this._logger.LogInformation(
            "Curator completion {Outcome} from model {Model} in {LatencyMilliseconds:0.000} ms with {InputCharacters} input characters, "
            + "{OutputCharacters} output characters, input tokens {InputTokens}, output tokens {OutputTokens}, cached input tokens "
            + "{CachedInputTokens}, and reasoning tokens {ReasoningTokens}.",
            diagnostics.ParseFailureReason is null ? "parsed" : "parseFailed",
            completion.Model,
            completion.LatencyMilliseconds,
            diagnostics.InputCharacters,
            diagnostics.OutputCharacters,
            completion.InputTokens,
            completion.OutputTokens,
            completion.CachedInputTokens,
            completion.ReasoningTokens);

        if (diagnostics.ParseFailureReason is not null)
        {
            this._logger.LogWarning(
                "Curator completion was recorded as a parse failure: {Reason}; output characters {OutputCharacters}.",
                diagnostics.ParseFailureReason,
                diagnostics.OutputCharacters);
        }
    }
}
