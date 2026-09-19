using System.Text.Json.Serialization;

namespace ChangeLens.Infrastructure.ModelCompletion.Models;

/// <summary>
///     Represents the JSON request body sent to an OpenAI-compatible chat-completions endpoint.
/// </summary>
internal sealed record OpenAiCompatibleChatCompletionRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiCompatibleChatMessage> Messages,
    [property: JsonPropertyName("max_completion_tokens")] int MaximumOutputTokens,
    [property: JsonPropertyName("response_format")] OpenAiCompatibleResponseFormat ResponseFormat,
    [property: JsonPropertyName("temperature")] double? Temperature,
    [property: JsonPropertyName("reasoning_effort")] string? ReasoningEffort);
