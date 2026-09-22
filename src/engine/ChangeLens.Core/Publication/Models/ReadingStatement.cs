namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents one published statement.</summary>
/// <param name="ClaimId">The stable claim identifier.</param>
/// <param name="Text">The statement text.</param>
/// <param name="Trust">The strongest trust level of its citations.</param>
/// <param name="EvidenceNodeIds">The evidence nodes cited by the statement.</param>
public sealed record ReadingStatement(string ClaimId, string Text, ReadingTrust Trust, IReadOnlyList<string> EvidenceNodeIds);
