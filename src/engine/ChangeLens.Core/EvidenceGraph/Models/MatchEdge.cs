namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Represents a non-semantic match between a changed-file node and a candidate node.
/// </summary>
/// <remarks>
///     An edge records shared text or history. It is not a dependency, invocation, or other behavioral relationship,
///     and it is never part of a model payload.
/// </remarks>
/// <param name="EdgeId">The stable edge id, such as <c>e001</c>. Cannot be <see langword="null" />.</param>
/// <param name="FromNodeId">The changed-file endpoint node id. Cannot be <see langword="null" />.</param>
/// <param name="ToNodeId">The candidate endpoint node id. Cannot be <see langword="null" />.</param>
/// <param name="Kind">The correspondence signal family that produced the edge.</param>
/// <param name="MatchedValue">The shared normalized key, or <see langword="null" /> for co-change.</param>
/// <param name="FromAnchor">Where the matched value sits relative to the changed-file endpoint.</param>
/// <param name="ToAnchor">Where the matched value sits relative to the candidate endpoint.</param>
/// <param name="DocumentFrequency">The number of indexed files holding the key, or <see langword="null" /> for co-change.</param>
/// <param name="Contribution">The correspondence contribution of the signal.</param>
public sealed record MatchEdge(
    string EdgeId,
    string FromNodeId,
    string ToNodeId,
    MatchEdgeKind Kind,
    string? MatchedValue,
    MatchEdgeAnchor FromAnchor,
    MatchEdgeAnchor ToAnchor,
    int? DocumentFrequency,
    double Contribution);
