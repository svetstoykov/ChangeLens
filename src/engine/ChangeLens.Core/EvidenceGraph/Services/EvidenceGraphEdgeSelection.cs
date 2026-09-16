using ChangeLens.Core.EvidenceGraph.Models;

namespace ChangeLens.Core.EvidenceGraph.Services;

/// <summary>
///     Represents the kept match edges and the diagnostics of the edge caps.
/// </summary>
/// <param name="Edges">The kept edges ordered by endpoint ids, kind, and matched value. Cannot be <see langword="null" />.</param>
/// <param name="PairDroppedCount">The number of edges removed by the per-node-pair cap.</param>
/// <param name="TotalDroppedCount">The number of edges removed by the total edge cap.</param>
/// <param name="ContributionFloor">The contribution of the weakest edge the total cap kept, or zero.</param>
/// <param name="MaxPairDroppedContribution">The strongest contribution removed by the per-pair cap, or zero.</param>
/// <param name="PairDroppedAboveFloorCount">The number of per-pair drops that outscored the kept-edge floor.</param>
internal sealed record EdgeSelection(
    IReadOnlyList<MatchEdge> Edges,
    int PairDroppedCount,
    int TotalDroppedCount,
    double ContributionFloor,
    double MaxPairDroppedContribution,
    int PairDroppedAboveFloorCount);
