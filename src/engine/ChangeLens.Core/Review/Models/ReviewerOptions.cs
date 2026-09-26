using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Review.Constants;

namespace ChangeLens.Core.Review.Models;

/// <summary>
///     Represents call and reply limits for the reviewer.
/// </summary>
public sealed class ReviewerOptions
{
    /// <summary>
    ///     Gets or sets the maximum accepted completion size in characters. The default is 176,000 characters.
    /// </summary>
    public int MaximumOutputCharacters { get; set; } = ReviewerConfigurationConstants.DefaultMaximumOutputCharacters;

    /// <summary>
    ///     Gets or sets the maximum output-token request. The default is 48,000 tokens.
    /// </summary>
    public int MaximumOutputTokens { get; set; } = ReviewerConfigurationConstants.DefaultMaximumOutputTokens;

    /// <summary>
    ///     Gets or sets the optional provider reasoning effort.
    /// </summary>
    public ModelReasoningEffort? ReasoningEffort { get; set; }
}
