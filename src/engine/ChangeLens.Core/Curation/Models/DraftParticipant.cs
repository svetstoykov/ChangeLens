namespace ChangeLens.Core.Curation.Models;

/// <summary>
///     Represents a participant in one curator track.
/// </summary>
/// <param name="Id">The participant identifier.</param>
/// <param name="Name">The participant display name.</param>
/// <param name="Role">The participant role.</param>
/// <param name="Changed">Whether the participant is changed in the captured comparison.</param>
/// <param name="EvidenceNodeIds">The evidence node identifiers cited by the participant.</param>
public sealed record DraftParticipant(
    string Id,
    string Name,
    string Role,
    bool Changed,
    IReadOnlyList<string> EvidenceNodeIds);
