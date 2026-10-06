namespace ChangeLens.Core.EvidenceGraph.Constants;

/// <summary>
///     Defines configuration keys for evidence graph construction.
/// </summary>
public static class EvidenceGraphConfigurationConstants
{
    /// <summary>
    ///     The configuration section containing evidence graph settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Analysis:EvidenceGraph";

    /// <summary>
    ///     The configuration key for the quote-window node budget.
    /// </summary>
    public const string MaximumQuoteWindowNodesKey = SectionKey + ":MaximumQuoteWindowNodes";

    /// <summary>
    ///     The configuration key for the quote windows one changed file takes in the first round of changed-file selection.
    /// </summary>
    public const string MaximumNodesPerChangedFileKey = SectionKey + ":MaximumNodesPerChangedFile";

    /// <summary>
    ///     The configuration key for the longest changed side, in lines, that is quoted whole.
    /// </summary>
    public const string MaximumWholeChangedFileLinesKey = SectionKey + ":MaximumWholeChangedFileLines";

    /// <summary>
    ///     The configuration key for the most lines a changed-hunk window grows on each side to reach a blank line.
    /// </summary>
    public const string MaximumBlockExtensionLinesKey = SectionKey + ":MaximumBlockExtensionLines";

    /// <summary>
    ///     The configuration key for the maximum kept match edges.
    /// </summary>
    public const string MaximumEdgesKey = SectionKey + ":MaximumEdges";
}
