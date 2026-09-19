namespace ChangeLens.Core.Curation.Models;

/// <summary>
///     Represents one bounded track in a curator draft.
/// </summary>
/// <param name="Id">The track identifier.</param>
/// <param name="Title">The track title.</param>
/// <param name="Summary">The evidence-bound track summary.</param>
/// <param name="Shape">The closed-vocabulary track shape.</param>
/// <param name="Participants">The track participants.</param>
/// <param name="Relationships">The participant relationships.</param>
/// <param name="OrderedSteps">The optional ordered steps.</param>
/// <param name="Purposes">The optional purpose cards.</param>
public sealed record DraftTrack(
    string Id,
    string Title,
    BoundStatement Summary,
    string Shape,
    IReadOnlyList<DraftParticipant> Participants,
    IReadOnlyList<DraftRelationship> Relationships,
    IReadOnlyList<BoundStatement> OrderedSteps,
    IReadOnlyList<BoundStatement> Purposes);
