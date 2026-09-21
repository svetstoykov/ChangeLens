using ChangeLens.Core.ModelCompletion.Models;

namespace ChangeLens.Core.Curation.Models;

/// <summary>
///     Represents call and reply limits for the curator.
/// </summary>
public sealed class CuratorOptions
{
    /// <summary>
    ///     Gets or sets the maximum accepted completion size in characters. The default is 176,000 characters.
    /// </summary>
    public int MaximumOutputCharacters { get; set; } = 176_000;

    /// <summary>
    ///     Gets or sets the maximum output-token request. The default is 48,000 tokens.
    /// </summary>
    public int MaximumOutputTokens { get; set; } = 48_000;

    /// <summary>
    ///     Gets or sets the optional provider reasoning effort.
    /// </summary>
    public ModelReasoningEffort? ReasoningEffort { get; set; }
}
