using ChangeLens.Core.EvidenceGraph.Models;

namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents a disclosed non-semantic match edge retained for engine-side checking.
/// </summary>
/// <param name="EdgeId">The stable edge id.</param>
/// <param name="FromNodeId">The source node id.</param>
/// <param name="ToNodeId">The destination node id.</param>
/// <param name="Kind">The correspondence signal family.</param>
/// <param name="MatchedValue">The matched value, or <see langword="null" /> when withheld.</param>
/// <param name="FromAnchor">The source endpoint anchor.</param>
/// <param name="ToAnchor">The destination endpoint anchor.</param>
/// <param name="Contribution">The engine-side ranking contribution.</param>
public sealed record BinderMatchEdge(
    string EdgeId,
    string FromNodeId,
    string ToNodeId,
    MatchEdgeKind Kind,
    string? MatchedValue,
    MatchEdgeAnchor FromAnchor,
    MatchEdgeAnchor ToAnchor,
    double Contribution);
