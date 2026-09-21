namespace ChangeLens.Core.ModelCompletion.Models;

/// <summary>
///     Represents one successful provider completion and its usage measurements.
/// </summary>
/// <param name="Model">The provider model slug that produced the completion.</param>
/// <param name="Text">The non-empty completion text.</param>
/// <param name="LatencyMilliseconds">The elapsed provider-call time in milliseconds.</param>
/// <param name="InputTokens">The reported input-token count, or <see langword="null" /> when absent.</param>
/// <param name="OutputTokens">The reported output-token count, or <see langword="null" /> when absent.</param>
/// <param name="CachedInputTokens">The reported cached-input-token count, or <see langword="null" /> when absent.</param>
/// <param name="ReasoningTokens">The reported reasoning-token count, or <see langword="null" /> when absent.</param>
public sealed record ModelCompletion(
    string Model,
    string Text,
    double LatencyMilliseconds,
    int? InputTokens,
    int? OutputTokens,
    int? CachedInputTokens,
    int? ReasoningTokens);
