namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Represents configurable bounds for evidence graph construction.
/// </summary>
/// <remarks>
///     The defaults are the values the analysis prototype measured. Every numeric value must be positive, except
///     <see cref="HunkContextLines" /> and <see cref="MergeGapLines" />, which may be zero, and
///     <see cref="ChangedNodeShare" />, which must be between zero and one.
/// </remarks>
public sealed class EvidenceGraphOptions
{
    /// <summary>
    ///     Gets or sets the quote-window node budget shared by changed files and candidates. Manifest facts do not count.
    ///     The default is 120 nodes.
    /// </summary>
    public int MaximumQuoteWindowNodes { get; set; } = 120;

    /// <summary>
    ///     Gets or sets the share of the quote-window budget reserved for changed files, rounded up. The default is 0.6.
    /// </summary>
    public double ChangedNodeShare { get; set; } = 0.6;

    /// <summary>
    ///     Gets or sets the unchanged context lines quoted on each side of a changed run. The default is three lines.
    /// </summary>
    public int HunkContextLines { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the maximum lines one node may quote. The default is 60 lines.
    /// </summary>
    public int MaximumQuotedLinesPerNode { get; set; } = 60;

    /// <summary>
    ///     Gets or sets the lines quoted around one candidate match line. The default is 12 lines.
    /// </summary>
    public int CandidateWindowLines { get; set; } = 12;

    /// <summary>
    ///     Gets or sets the largest gap between two windows that still merges them. The default is two lines.
    /// </summary>
    public int MergeGapLines { get; set; } = 2;

    /// <summary>
    ///     Gets or sets the maximum quote windows selected for one changed file. The default is four nodes.
    /// </summary>
    public int MaximumNodesPerChangedFile { get; set; } = 4;

    /// <summary>
    ///     Gets or sets the maximum quote windows selected for one candidate. The default is three nodes.
    /// </summary>
    public int MaximumNodesPerCandidate { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the maximum head-of-file windows for candidates without a quotable match line. The default is
    ///     eight nodes.
    /// </summary>
    public int MaximumCandidateHeadNodes { get; set; } = 8;

    /// <summary>
    ///     Gets or sets the maximum kept match edges. The default is 400 edges.
    /// </summary>
    public int MaximumEdges { get; set; } = 400;

    /// <summary>
    ///     Gets or sets the maximum kept match edges between one pair of nodes. The default is three edges.
    /// </summary>
    public int MaximumEdgesPerNodePair { get; set; } = 3;
}
