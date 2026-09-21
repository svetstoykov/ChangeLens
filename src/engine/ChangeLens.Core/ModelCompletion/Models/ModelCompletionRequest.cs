namespace ChangeLens.Core.ModelCompletion.Models;

/// <summary>
///     Represents the provider-neutral input for one JSON completion.
/// </summary>
/// <param name="SystemMessage">The system instruction for the completion.</param>
/// <param name="UserMessage">The user payload for the completion.</param>
/// <param name="MaximumOutputTokens">The maximum output-token count sent to the provider.</param>
/// <param name="ReasoningEffort">The optional provider reasoning effort.</param>
public sealed record ModelCompletionRequest(
    string SystemMessage,
    string UserMessage,
    int MaximumOutputTokens,
    ModelReasoningEffort? ReasoningEffort = null);
