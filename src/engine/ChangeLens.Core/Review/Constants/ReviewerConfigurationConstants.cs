namespace ChangeLens.Core.Review.Constants;

/// <summary>
///     Defines the default limits for reviewer completions.
/// </summary>
public static class ReviewerConfigurationConstants
{
    /// <summary>
    ///     Gets the default completion size limit in characters.
    /// </summary>
    public const int DefaultMaximumOutputCharacters = 176_000;

    /// <summary>
    ///     Gets the default completion token limit.
    /// </summary>
    public const int DefaultMaximumOutputTokens = 48_000;
}
