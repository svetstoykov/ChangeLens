using ChangeLens.Core.ModelCompletion.Models;

namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Represents claim-checking configuration and the model-backed checker's call limits.
/// </summary>
public sealed class ClaimCheckingOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether checking is enabled. The default is <see langword="false" />.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of claims submitted in one checker call. The default is 120 claims.
    /// </summary>
    public int MaximumClaims { get; set; } = 120;

    /// <summary>
    ///     Gets or sets the maximum claims-payload size in characters. The default is 368,000 characters.
    /// </summary>
    public int MaximumPayloadCharacters { get; set; } = 368_000;

    /// <summary>
    ///     Gets or sets the number of completion attempts made while no verdict can be read. The default is 2 attempts.
    /// </summary>
    public int Attempts { get; set; } = 2;

    /// <summary>
    ///     Gets or sets the optional provider reasoning effort. The default is <see cref="ModelReasoningEffort.Low" />.
    /// </summary>
    public ModelReasoningEffort? ReasoningEffort { get; set; } = ModelReasoningEffort.Low;

    /// <summary>
    ///     Gets or sets the maximum output-token request, which also bounds reasoning tokens. The default is 48,000 tokens.
    /// </summary>
    public int MaximumOutputTokens { get; set; } = 48_000;
}
