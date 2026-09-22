namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one relationship between two participants.
/// </summary>
/// <param name="ClaimId">The stable claim identifier.</param>
/// <param name="Id">The relationship identifier.</param>
/// <param name="FromParticipantId">The source participant identifier.</param>
/// <param name="ToParticipantId">The target participant identifier.</param>
/// <param name="Kind">The curator relationship kind, passed through unchanged.</param>
/// <param name="Explanation">The relationship explanation.</param>
/// <param name="Trust">The trust wire value.</param>
/// <param name="EvidenceNodeIds">The disclosed evidence node identifiers the relationship cites.</param>
internal sealed record ReadingRelationshipResult(
    string ClaimId,
    string Id,
    string FromParticipantId,
    string ToParticipantId,
    string Kind,
    string Explanation,
    string Trust,
    IReadOnlyList<string> EvidenceNodeIds);
