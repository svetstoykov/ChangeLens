using System.Text.Json.Serialization;

namespace ChangeLens.Infrastructure.ModelCompletion.Models;

/// <summary>
///     Represents one message in an OpenAI-compatible chat-completions request.
/// </summary>
internal sealed record OpenAiCompatibleChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);
