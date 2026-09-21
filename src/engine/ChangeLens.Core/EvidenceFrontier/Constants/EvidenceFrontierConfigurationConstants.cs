namespace ChangeLens.Core.EvidenceFrontier.Constants;

/// <summary>
///     Defines configuration keys for evidence frontier construction.
/// </summary>
public static class EvidenceFrontierConfigurationConstants
{
    /// <summary>
    ///     The configuration section containing evidence frontier settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Analysis:Frontier";

    /// <summary>
    ///     The configuration key for the maximum returned frontier entries.
    /// </summary>
    public const string MaximumEntriesKey = SectionKey + ":MaximumEntries";
}
