namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents one published participant relationship.</summary>
/// <param name="ClaimId">The stable claim identifier.</param>
/// <param name="Id">The relationship identifier.</param>
/// <param name="FromParticipantId">The source participant identifier.</param>
/// <param name="ToParticipantId">The target participant identifier.</param>
/// <param name="Kind">The relationship kind.</param>
/// <param name="Explanation">The relationship explanation.</param>
/// <param name="Trust">The strongest trust level of its citations.</param>
/// <param name="EvidenceNodeIds">The evidence nodes cited by the relationship.</param>
public sealed record ReadingRelationship(
    string ClaimId,
    string Id,
    string FromParticipantId,
    string ToParticipantId,
    string Kind,
    string Explanation,
    ReadingTrust Trust,
    IReadOnlyList<string> EvidenceNodeIds);
