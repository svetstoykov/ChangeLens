namespace ChangeLens.Infrastructure.ModelCompletion.Models;

/// <summary>
///     Holds the parsed completion envelope before the adapter adds its measured latency.
/// </summary>
/// <remarks>
///     <see cref="Text" /> is <see langword="null" /> or blank when the reply carried no usable content. The identifier fields hold
///     only short, plain identifiers and are <see langword="null" /> when the envelope omits them or carries anything else.
/// </remarks>
internal sealed record ParsedModelCompletion(
    string Model,
    string? Text,
    int? InputTokens,
    int? OutputTokens,
    int? CachedInputTokens,
    int? ReasoningTokens,
    string? FinishReason,
    string? Upstream,
    string? GenerationId,
    int ReasoningCharacters);
