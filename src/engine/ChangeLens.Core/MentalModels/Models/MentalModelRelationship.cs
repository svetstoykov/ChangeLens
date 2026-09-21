namespace ChangeLens.Core.MentalModels.Models;

/// <summary>
///     Represents a published relationship between two track participants.
/// </summary>
/// <param name="ClaimId">The stable claim identifier.</param>
/// <param name="Id">The curator relationship identifier.</param>
/// <param name="FromParticipantId">The source participant identifier.</param>
/// <param name="ToParticipantId">The target participant identifier.</param>
/// <param name="Kind">The published relationship kind.</param>
/// <param name="Explanation">The published relationship explanation.</param>
/// <param name="EvidenceNodeIds">The evidence nodes cited by the relationship.</param>
/// <param name="MatchEdgeIds">The engine match edges retained for the relationship.</param>
/// <param name="Focus">The resolved checker focus ranges.</param>
public sealed record MentalModelRelationship(
    string ClaimId,
    string Id,
    string FromParticipantId,
    string ToParticipantId,
    string Kind,
    string Explanation,
    IReadOnlyList<string> EvidenceNodeIds,
    IReadOnlyList<string> MatchEdgeIds,
    IReadOnlyList<FocusRange> Focus);
