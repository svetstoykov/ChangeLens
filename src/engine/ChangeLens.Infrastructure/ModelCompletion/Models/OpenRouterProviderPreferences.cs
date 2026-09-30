using System.Text.Json.Serialization;

namespace ChangeLens.Infrastructure.ModelCompletion.Models;

/// <summary>
///     Represents OpenRouter's upstream routing preferences in a chat-completions request.
/// </summary>
/// <param name="Sort">The upstream ordering, or <see langword="null" /> for OpenRouter's default.</param>
/// <param name="Ignore">The upstream names never to route to, or <see langword="null" /> for none.</param>
internal sealed record OpenRouterProviderPreferences(
    [property: JsonPropertyName("sort")] string? Sort,
    [property: JsonPropertyName("ignore")] IReadOnlyList<string>? Ignore);
