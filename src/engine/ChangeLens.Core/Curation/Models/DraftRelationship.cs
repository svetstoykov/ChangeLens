namespace ChangeLens.Core.Curation.Models;

/// <summary>
///     Represents a relationship between two participants in one curator track.
/// </summary>
/// <param name="Id">The relationship identifier.</param>
/// <param name="FromParticipantId">The source participant identifier.</param>
/// <param name="ToParticipantId">The target participant identifier.</param>
/// <param name="Kind">The closed-vocabulary relationship kind.</param>
/// <param name="Explanation">The relationship explanation.</param>
/// <param name="EvidenceNodeIds">The evidence node identifiers cited by the relationship.</param>
/// <param name="MatchEdgeIds">The engine match-edge identifiers cited by the relationship.</param>
public sealed record DraftRelationship(
    string Id,
    string FromParticipantId,
    string ToParticipantId,
    string Kind,
    string Explanation,
    IReadOnlyList<string> EvidenceNodeIds,
    IReadOnlyList<string> MatchEdgeIds);
