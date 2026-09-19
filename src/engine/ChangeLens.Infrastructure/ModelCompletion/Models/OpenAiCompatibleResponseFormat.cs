using System.Text.Json.Serialization;

namespace ChangeLens.Infrastructure.ModelCompletion.Models;

/// <summary>
///     Represents the structured JSON response format requested from an OpenAI-compatible provider.
/// </summary>
internal sealed record OpenAiCompatibleResponseFormat([property: JsonPropertyName("type")] string Type);
