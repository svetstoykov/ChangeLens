using ChangeLens.Core.Curation.Models;

namespace ChangeLens.Core.MentalModels.Models;

/// <summary>
///     Represents a published mental-model track.
/// </summary>
/// <param name="Id">The track identifier.</param>
/// <param name="Title">The track title.</param>
/// <param name="Summary">The checked track summary, or <see langword="null" />.</param>
/// <param name="Shape">The track shape.</param>
/// <param name="Participants">The surviving track participants.</param>
/// <param name="Relationships">The surviving track relationships.</param>
/// <param name="OrderedSteps">The surviving ordered steps.</param>
/// <param name="Purposes">The surviving purpose cards.</param>
public sealed record MentalModelTrack(
    string Id,
    string Title,
    MentalModelStatement? Summary,
    string Shape,
    IReadOnlyList<DraftParticipant> Participants,
    IReadOnlyList<MentalModelRelationship> Relationships,
    IReadOnlyList<MentalModelStatement> OrderedSteps,
    IReadOnlyList<MentalModelStatement> Purposes);
