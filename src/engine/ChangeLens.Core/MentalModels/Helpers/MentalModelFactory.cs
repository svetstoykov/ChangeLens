using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.MentalModels.Models;

namespace ChangeLens.Core.MentalModels.Helpers;

/// <summary>
///     Provides conversion from a validated curator draft to a published mental model.
/// </summary>
public static class MentalModelFactory
{
    /// <summary>
    ///     Creates a published mental model and assigns every claim id exactly once.
    /// </summary>
    /// <param name="draft">The validated curator draft.</param>
    /// <returns>The published mental model.</returns>
    public static MentalModel FromDraft(MentalModelDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Tracks.Count == 0)
        {
            return MentalModel.Empty;
        }

        var thesis = string.IsNullOrWhiteSpace(draft.Thesis.Text)
            ? null
            : ToStatement(ClaimIds.Thesis, draft.Thesis);
        var tracks = draft.Tracks.Select(ToTrack).ToArray();
        return new MentalModel(thesis, tracks);
    }

    private static MentalModelTrack ToTrack(DraftTrack track)
    {
        var summary = ToStatement(ClaimIds.Summary(track.Id), track.Summary);
        var steps = track.OrderedSteps.Select((statement, index) =>
            ToStatement(ClaimIds.Step(track.Id, index), statement)).ToArray();
        var purposes = track.Purposes.Select((statement, index) =>
            ToStatement(ClaimIds.Purpose(track.Id, index), statement)).ToArray();
        var relationships = track.Relationships.Select(relationship => new MentalModelRelationship(
            ClaimIds.Relationship(track.Id, relationship.Id),
            relationship.Id,
            relationship.FromParticipantId,
            relationship.ToParticipantId,
            relationship.Kind,
            relationship.Explanation,
            relationship.EvidenceNodeIds,
            relationship.MatchEdgeIds,
            [])).ToArray();
        return new MentalModelTrack(
            track.Id,
            track.Title,
            summary,
            track.Shape,
            track.Participants,
            relationships,
            steps,
            purposes);
    }

    private static MentalModelStatement ToStatement(string claimId, BoundStatement statement) =>
        new(claimId, statement.Text, statement.EvidenceNodeIds, []);
}
