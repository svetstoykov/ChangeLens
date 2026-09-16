namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Represents the bounded, undisclosed quotes and match edges built for one captured change.
/// </summary>
/// <remarks>
///     Nothing in this graph is safe to send to a model. Context policy decides what is disclosed.
/// </remarks>
/// <param name="Nodes">Manifest fact nodes first, then changed and candidate quote windows. Cannot be <see langword="null" />.</param>
/// <param name="Edges">The kept match edges ordered by endpoint ids, kind, and matched value. Cannot be <see langword="null" />.</param>
/// <param name="Diagnostics">The budget, cap, and skip diagnostics. Cannot be <see langword="null" />.</param>
public sealed record EvidenceGraph(
    IReadOnlyList<EvidenceNode> Nodes,
    IReadOnlyList<MatchEdge> Edges,
    EvidenceGraphDiagnostics Diagnostics);
