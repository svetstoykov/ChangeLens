namespace ChangeLens.Core.MentalModels.Constants;

/// <summary>
///     Defines the claim-id formats used by published mental models.
/// </summary>
public static class ClaimIdConstants
{
    /// <summary>
    ///     Gets the claim id assigned to a published thesis.
    /// </summary>
    public const string Thesis = "thesis";

    /// <summary>
    ///     Gets the separator used between claim-id components.
    /// </summary>
    public const string Separator = ":";

    /// <summary>
    ///     Gets the format for a track-summary claim id.
    /// </summary>
    public const string TrackSummaryFormat = "track:{0}:summary";

    /// <summary>
    ///     Gets the format for an ordered-step claim id.
    /// </summary>
    public const string OrderedStepFormat = "track:{0}:step:{1}";

    /// <summary>
    ///     Gets the format for a purpose claim id.
    /// </summary>
    public const string PurposeFormat = "track:{0}:purpose:{1}";

    /// <summary>
    ///     Gets the format for a relationship claim id.
    /// </summary>
    public const string RelationshipFormat = "track:{0}:relationship:{1}";

    /// <summary>
    ///     Gets the format for a participant claim id.
    /// </summary>
    public const string ParticipantFormat = "track:{0}:participant:{1}";
}
