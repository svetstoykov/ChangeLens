namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Represents the disclosure result for one evidence graph.
/// </summary>
/// <param name="Decisions">One decision per graph node in graph order. Cannot be <see langword="null" />.</param>
/// <param name="EdgeDecisions">One decision per graph edge in graph order. Cannot be <see langword="null" />.</param>
/// <param name="DisclosedNodes">The allowed and redacted nodes in graph order. Cannot be <see langword="null" />.</param>
/// <param name="DisclosedEdges">The edges whose endpoints are both disclosed. Cannot be <see langword="null" />.</param>
/// <param name="Diagnostics">The disclosure counts. Cannot be <see langword="null" />.</param>
public sealed record ContextPolicyOutcome(
    IReadOnlyList<ContextPolicyDecision> Decisions,
    IReadOnlyList<ContextPolicyEdgeDecision> EdgeDecisions,
    IReadOnlyList<DisclosedEvidenceNode> DisclosedNodes,
    IReadOnlyList<DisclosedMatchEdge> DisclosedEdges,
    ContextPolicyDiagnostics Diagnostics);
