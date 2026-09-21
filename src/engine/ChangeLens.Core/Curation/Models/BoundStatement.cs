namespace ChangeLens.Core.Curation.Models;

/// <summary>
///     Represents a curator statement bound to disclosed evidence nodes.
/// </summary>
/// <param name="Text">The statement text.</param>
/// <param name="EvidenceNodeIds">The evidence node identifiers cited by the statement.</param>
public sealed record BoundStatement(string Text, IReadOnlyList<string> EvidenceNodeIds);
