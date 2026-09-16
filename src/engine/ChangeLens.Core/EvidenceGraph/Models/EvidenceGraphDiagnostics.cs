namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Represents budget, cap, and skip diagnostics for one evidence graph.
/// </summary>
/// <remarks>
///     The kept-edge floor answers whether the per-pair edge cap removed an edge that outscored the weakest edge the
///     total cap kept. A non-zero <see cref="PairDroppedAboveFloorCount" /> means it did.
/// </remarks>
/// <param name="NodeCount">The number of nodes, manifest facts included.</param>
/// <param name="ManifestFactNodeCount">The number of manifest fact nodes, which take no quote-window slot.</param>
/// <param name="EdgeCount">The number of kept match edges.</param>
/// <param name="QuotedLineCount">The number of quoted lines across quote windows.</param>
/// <param name="WindowMergeCount">The number of times one window was merged into a neighbor.</param>
/// <param name="ChangedFileCount">The number of analyzed changed files.</param>
/// <param name="RepresentedChangedFileCount">The number of changed files with at least one node.</param>
/// <param name="CandidateCount">The number of ranked candidates.</param>
/// <param name="RepresentedCandidateCount">The number of candidates with at least one node.</param>
/// <param name="CandidatesWithoutQuotableLine">The number of readable candidates with no match line to quote.</param>
/// <param name="TruncatedNodeCount">The number of nodes split by the per-node line cap.</param>
/// <param name="DroppedWindowCount">The number of merged quote windows left out by the quote-window budget.</param>
/// <param name="ChangedBudget">The initial quote-window slots reserved for changed files.</param>
/// <param name="CandidateBudget">The initial quote-window slots reserved for candidates.</param>
/// <param name="PairDroppedEdgeCount">The number of edges removed by the per-node-pair cap.</param>
/// <param name="TotalDroppedEdgeCount">The number of edges removed by the total edge cap.</param>
/// <param name="KeptEdgeContributionFloor">The contribution of the weakest edge the total cap kept, or zero.</param>
/// <param name="MaxPairDroppedContribution">The strongest contribution removed by the per-pair cap, or zero.</param>
/// <param name="PairDroppedAboveFloorCount">The number of per-pair drops that outscored the kept-edge floor.</param>
/// <param name="NodesByOrigin">The node counts by origin name. Cannot be <see langword="null" />.</param>
/// <param name="EdgesByKind">The kept edge counts by kind name. Cannot be <see langword="null" />.</param>
/// <param name="SkipReasons">The skip and dropped-edge counts by short reason. Cannot be <see langword="null" />.</param>
public sealed record EvidenceGraphDiagnostics(
    int NodeCount,
    int ManifestFactNodeCount,
    int EdgeCount,
    int QuotedLineCount,
    int WindowMergeCount,
    int ChangedFileCount,
    int RepresentedChangedFileCount,
    int CandidateCount,
    int RepresentedCandidateCount,
    int CandidatesWithoutQuotableLine,
    int TruncatedNodeCount,
    int DroppedWindowCount,
    int ChangedBudget,
    int CandidateBudget,
    int PairDroppedEdgeCount,
    int TotalDroppedEdgeCount,
    double KeptEdgeContributionFloor,
    double MaxPairDroppedContribution,
    int PairDroppedAboveFloorCount,
    IReadOnlyDictionary<string, int> NodesByOrigin,
    IReadOnlyDictionary<string, int> EdgesByKind,
    IReadOnlyDictionary<string, int> SkipReasons)
{
    /// <summary>
    ///     Gets the number of edges removed by either edge cap.
    /// </summary>
    public int DroppedEdgeCount => this.PairDroppedEdgeCount + this.TotalDroppedEdgeCount;
}
