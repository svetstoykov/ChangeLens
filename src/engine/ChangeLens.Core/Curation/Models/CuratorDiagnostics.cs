namespace ChangeLens.Core.Curation.Models;

/// <summary>
///     Represents provider and parsing diagnostics retained with one curator outcome.
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
/// <param name="DroppedNodeIds">The node ids the raw draft marked as dropped.</param>
/// <param name="RawReply">The raw provider reply retained for inspection.</param>
/// <param name="SystemMessage">The rendered system message used for the call.</param>
public sealed record CuratorDiagnostics(
    string Model,
    double LatencyMilliseconds,
    int InputCharacters,
    int OutputCharacters,
    int? InputTokens,
    int? OutputTokens,
    int? CachedInputTokens,
    int? ReasoningTokens,
    string? ParseFailureReason,
    IReadOnlyList<string> DroppedNodeIds,
    string RawReply,
    string SystemMessage);
