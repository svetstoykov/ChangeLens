namespace ChangeLens.Core.EvidenceBinder.Constants;

/// <summary>
///     Defines configuration keys for evidence binder assembly.
/// </summary>
public static class EvidenceBinderConfigurationConstants
{
    /// <summary>
    ///     The configuration section containing evidence binder settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Analysis:EvidenceBinder";

    /// <summary>
    ///     The context window size in provider tokens.
    /// </summary>
    public const string ContextWindowTokensKey = SectionKey + ":ContextWindowTokens";

    /// <summary>
    ///     The reserved curator output size in characters.
    /// </summary>
    public const string CuratorOutputCharactersKey = SectionKey + ":CuratorOutputCharacters";

    /// <summary>
    ///     The reserved curator prompt size in characters.
    /// </summary>
    public const string PromptReserveCharactersKey = SectionKey + ":PromptReserveCharacters";

    /// <summary>
    ///     The optional hard binder character cap.
    /// </summary>
    public const string MaximumBinderCharactersKey = SectionKey + ":MaximumBinderCharacters";

    /// <summary>
    ///     The fraction of the hard cap used as the ordinary ladder target.
    /// </summary>
    public const string TargetUtilizationKey = SectionKey + ":TargetUtilization";
}
