using ChangeLens.Core.Curation.Models;

namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents one participant in a published area.</summary>
/// <param name="Id">The participant identifier.</param>
/// <param name="Name">The participant display name.</param>
/// <param name="Role">The participant role.</param>
/// <param name="Changed">Whether the participant is changed.</param>
/// <param name="EvidenceNodeIds">The participant's cited evidence nodes.</param>
public sealed record ReadingParticipant(string Id, string Name, string Role, bool Changed, IReadOnlyList<string> EvidenceNodeIds)
{
    /// <summary>Creates a published participant from the validated draft participant.</summary>
    /// <param name="participant">The draft participant.</param>
    /// <returns>The published participant.</returns>
    public static ReadingParticipant FromDraft(DraftParticipant participant) =>
        new(participant.Id, participant.Name, participant.Role, participant.Changed, participant.EvidenceNodeIds);
}
