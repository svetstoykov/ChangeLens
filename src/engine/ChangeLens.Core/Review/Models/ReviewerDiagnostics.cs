namespace ChangeLens.Core.Review.Models;

/// <summary>
///     Represents provider and parsing diagnostics retained with one reviewer outcome.
/// </summary>
/// <param name="Model">The provider model slug.</param>
/// <param name="LatencyMilliseconds">The completion latency in milliseconds.</param>
/// <param name="InputCharacters">The combined system and user message character count.</param>
/// <param name="OutputCharacters">The raw completion character count.</param>
/// <param name="InputTokens">The provider-reported input-token count, or <see langword="null" />.</param>
/// <param name="OutputTokens">The provider-reported output-token count, or <see langword="null" />.</param>
/// <param name="CachedInputTokens">The provider-reported cached-input-token count, or <see langword="null" />.</param>
/// <param name="ReasoningTokens">The provider-reported reasoning-token count, or <see langword="null" />.</param>
/// <param name="ParseFailureReason">The parse or output-cap failure reason, or <see langword="null" />.</param>
public sealed record ReviewerDiagnostics(
    string Model,
    double LatencyMilliseconds,
    int InputCharacters,
    int OutputCharacters,
    int? InputTokens,
    int? OutputTokens,
    int? CachedInputTokens,
    int? ReasoningTokens,
    string? ParseFailureReason);
