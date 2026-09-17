namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Represents configurable bounds for context policy disclosure.
/// </summary>
/// <remarks>
///     The defaults are the values the analysis prototype measured. Every numeric value must be positive, and the share
///     must not exceed one. The per-node line limit is the evidence graph's quoted-line cap.
/// </remarks>
public sealed class ContextPolicyOptions
{
    /// <summary>
    ///     Gets or sets the maximum UTF-16 characters one node may disclose. The default is 8,000 characters.
    /// </summary>
    public int MaximumDisclosedCharactersPerNode { get; set; } = 8_000;

    /// <summary>
    ///     Gets or sets the largest share of a node's lines that may be redacted before the node is excluded. The default
    ///     is 0.5.
    /// </summary>
    public double MaximumRedactedLineShare { get; set; } = 0.5;

    /// <summary>
    ///     Gets or sets the shortest captured value a value-capturing secret pattern treats as a secret. The default is
    ///     eight characters.
    /// </summary>
    public int MinimumSecretValueLength { get; set; } = 8;
}
