namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one published reading area.
/// </summary>
/// <param name="Id">The track identifier.</param>
/// <param name="Title">The area title.</param>
/// <param name="Summary">The area summary, or <see langword="null" /> when none survived validation.</param>
/// <param name="Shape">The repaired shape wire value.</param>
/// <param name="Participants">The participants of the area.</param>
/// <param name="Relationships">The relationships carried by a participant-map area.</param>
/// <param name="OrderedSteps">The ordered steps carried by a walk area.</param>
/// <param name="Purposes">The purposes carried by a purpose-cards area.</param>
internal sealed record ReadingAreaResult(
    string Id,
    string Title,
    ReadingStatementResult? Summary,
    string Shape,
    IReadOnlyList<ReadingParticipantResult> Participants,
    IReadOnlyList<ReadingRelationshipResult> Relationships,
    IReadOnlyList<ReadingStatementResult> OrderedSteps,
    IReadOnlyList<ReadingStatementResult> Purposes);
