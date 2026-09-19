namespace ChangeLens.Core.Curation.Constants;

/// <summary>
///     Defines configuration keys for curator calls.
/// </summary>
public static class CuratorConfigurationConstants
{
    /// <summary>
    ///     The configuration section containing curator call settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Analysis:Curator";

    /// <summary>
    ///     The maximum accepted completion size in characters.
    /// </summary>
    public const string MaximumOutputCharactersKey = SectionKey + ":MaximumOutputCharacters";

    /// <summary>
    ///     The maximum output-token request sent to the provider.
    /// </summary>
    public const string MaximumOutputTokensKey = SectionKey + ":MaximumOutputTokens";

    /// <summary>
    ///     The optional provider reasoning effort.
    /// </summary>
    public const string ReasoningEffortKey = SectionKey + ":ReasoningEffort";
}
