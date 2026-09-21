using System.Globalization;
using ChangeLens.Core.MentalModels.Constants;

namespace ChangeLens.Core.MentalModels.Helpers;

/// <summary>
///     Provides stable claim identifiers for published mental-model members.
/// </summary>
public static class ClaimIds
{
    /// <summary>
    ///     Gets the thesis claim id.
    /// </summary>
    /// <returns>The thesis claim id.</returns>
    public const string Thesis = ClaimIdConstants.Thesis;

    /// <summary>
    ///     Creates a track-summary claim id using the short prototype-compatible name.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>The track-summary claim id.</returns>
    public static string Summary(string trackId) => TrackSummary(trackId);

    /// <summary>
    ///     Creates an ordered-step claim id using the short prototype-compatible name.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="index">The validated draft position.</param>
    /// <returns>The ordered-step claim id.</returns>
    public static string Step(string trackId, int index) => OrderedStep(trackId, index);

    /// <summary>
    ///     Creates a track-summary claim id.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>The track-summary claim id.</returns>
    public static string TrackSummary(string trackId) => string.Format(CultureInfo.InvariantCulture, ClaimIdConstants.TrackSummaryFormat, trackId);

    /// <summary>
    ///     Creates an ordered-step claim id.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="index">The validated draft position.</param>
    /// <returns>The ordered-step claim id.</returns>
    public static string OrderedStep(string trackId, int index) =>
        string.Format(CultureInfo.InvariantCulture, ClaimIdConstants.OrderedStepFormat, trackId, index);

    /// <summary>
    ///     Creates a purpose claim id.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="index">The validated draft position.</param>
    /// <returns>The purpose claim id.</returns>
    public static string Purpose(string trackId, int index) =>
        string.Format(CultureInfo.InvariantCulture, ClaimIdConstants.PurposeFormat, trackId, index);

    /// <summary>
    ///     Creates a relationship claim id.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="relationshipId">The relationship identifier.</param>
    /// <returns>The relationship claim id.</returns>
    public static string Relationship(string trackId, string relationshipId) =>
        string.Format(CultureInfo.InvariantCulture, ClaimIdConstants.RelationshipFormat, trackId, relationshipId);

    /// <summary>
    ///     Creates a participant claim id.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="participantId">The participant identifier.</param>
    /// <returns>The participant claim id.</returns>
    public static string Participant(string trackId, string participantId) =>
        string.Format(CultureInfo.InvariantCulture, ClaimIdConstants.ParticipantFormat, trackId, participantId);
}
