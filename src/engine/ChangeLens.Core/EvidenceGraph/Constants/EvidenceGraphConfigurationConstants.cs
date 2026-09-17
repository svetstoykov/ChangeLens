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
    ///     The configuration key for the maximum kept match edges.
    /// </summary>
    public const string MaximumEdgesKey = SectionKey + ":MaximumEdges";
}
