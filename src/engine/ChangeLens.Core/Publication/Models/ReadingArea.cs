namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents one published mental-model track.</summary>
/// <param name="Id">The track identifier.</param>
/// <param name="Title">The track title.</param>
/// <param name="Summary">The published track summary, or <see langword="null" />.</param>
/// <param name="Shape">The repaired rendering shape.</param>
/// <param name="Participants">The retained participants.</param>
/// <param name="Relationships">The retained relationships.</param>
/// <param name="OrderedSteps">The retained ordered steps.</param>
/// <param name="Purposes">The retained purposes.</param>
public sealed record ReadingArea(
    string Id,
    string Title,
    ReadingStatement? Summary,
    ReadingShape Shape,
    IReadOnlyList<ReadingParticipant> Participants,
    IReadOnlyList<ReadingRelationship> Relationships,
    IReadOnlyList<ReadingStatement> OrderedSteps,
    IReadOnlyList<ReadingStatement> Purposes);
