namespace ChangeLens.Core.ChangeAnatomy.Constants;

/// <summary>
///     Defines configuration keys for deterministic change anatomy extraction.
/// </summary>
public static class ChangeAnatomyConfigurationConstants
{
    /// <summary>
    ///     The configuration section containing change anatomy settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Analysis:ChangeAnatomy";

    /// <summary>
    ///     The configuration key for the minimum emitted key length.
    /// </summary>
    public const string MinimumKeyLengthKey = SectionKey + ":MinimumKeyLength";

    /// <summary>
    ///     The configuration key for the maximum distinct keys emitted per changed file.
    /// </summary>
    public const string MaximumKeysPerFileKey = SectionKey + ":MaximumKeysPerFile";

    /// <summary>
    ///     The configuration key for the maximum occurrences retained for one key in a file.
    /// </summary>
    public const string MaximumOccurrencesPerKeyKey = SectionKey + ":MaximumOccurrencesPerKey";
}
