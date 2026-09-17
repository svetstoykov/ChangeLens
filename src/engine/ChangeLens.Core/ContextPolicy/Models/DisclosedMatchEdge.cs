using ChangeLens.Core.EvidenceGraph.Models;

namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Represents a match edge whose endpoints are both disclosed.
/// </summary>
/// <remarks>
///     The edge stays inside the engine as ranking context. It is not behavior and is not serialized into a model payload.
/// </remarks>
/// <param name="EdgeId">The match edge id. Cannot be <see langword="null" />.</param>
/// <param name="FromNodeId">The changed-file endpoint node id. Cannot be <see langword="null" />.</param>
/// <param name="ToNodeId">The candidate endpoint node id. Cannot be <see langword="null" />.</param>
/// <param name="Kind">The correspondence signal family that produced the edge.</param>
/// <param name="MatchedValue">The matched value when a disclosed endpoint text contains it; otherwise <see langword="null" />.</param>
/// <param name="FromAnchor">Where the matched value sits relative to the changed-file endpoint.</param>
/// <param name="ToAnchor">Where the matched value sits relative to the candidate endpoint.</param>
/// <param name="Contribution">The correspondence contribution of the signal.</param>
public sealed record DisclosedMatchEdge(
    string EdgeId,
    string FromNodeId,
    string ToNodeId,
    MatchEdgeKind Kind,
    string? MatchedValue,
    MatchEdgeAnchor FromAnchor,
    MatchEdgeAnchor ToAnchor,
    double Contribution);
