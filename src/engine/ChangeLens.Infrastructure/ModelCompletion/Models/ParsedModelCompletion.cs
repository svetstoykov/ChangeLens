namespace ChangeLens.Infrastructure.ModelCompletion.Models;

/// <summary>
///     Holds the parsed completion envelope before the adapter adds its measured latency.
/// </summary>
internal sealed record ParsedModelCompletion(
    string Model,
    string Text,
    int? InputTokens,
    int? OutputTokens,
    int? CachedInputTokens,
    int? ReasoningTokens);
