using ChangeLens.Core.EvidenceGraph.Models;

namespace ChangeLens.Core.EvidenceGraph.Services;

/// <summary>
///     Represents a match edge before caps and identifiers are applied.
/// </summary>
/// <param name="FromNodeId">The changed-file endpoint node id. Cannot be <see langword="null" />.</param>
/// <param name="ToNodeId">The candidate endpoint node id. Cannot be <see langword="null" />.</param>
/// <param name="Kind">The correspondence signal family that produced the edge.</param>
/// <param name="MatchedValue">The shared normalized key, or <see langword="null" /> for co-change.</param>
/// <param name="FromAnchor">Where the matched value sits relative to the changed-file endpoint.</param>
/// <param name="ToAnchor">Where the matched value sits relative to the candidate endpoint.</param>
/// <param name="DocumentFrequency">The number of indexed files holding the key, or <see langword="null" />.</param>
/// <param name="Contribution">The correspondence contribution of the signal.</param>
internal sealed record EvidenceGraphPendingEdge(
    string FromNodeId,
    string ToNodeId,
    MatchEdgeKind Kind,
    string? MatchedValue,
    MatchEdgeAnchor FromAnchor,
    MatchEdgeAnchor ToAnchor,
    int? DocumentFrequency,
    double Contribution);
