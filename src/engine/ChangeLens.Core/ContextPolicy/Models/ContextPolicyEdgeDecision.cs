namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Represents the disclosure decision for one match edge.
/// </summary>
/// <param name="EdgeId">The match edge id. Cannot be <see langword="null" />.</param>
/// <param name="IsDisclosed">Whether both endpoints are disclosed, so the edge is disclosed.</param>
/// <param name="MatchedValueWithheld">Whether the disclosed edge withholds a matched value that no disclosed endpoint text contains.</param>
public sealed record ContextPolicyEdgeDecision(string EdgeId, bool IsDisclosed, bool MatchedValueWithheld);
