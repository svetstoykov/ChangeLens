namespace ChangeLens.Core.ClaimChecking.Constants;

/// <summary>
///     Defines the Type values accepted by the checker port.
/// </summary>
public static class CheckerClaimType
{
    /// <summary>
    ///     Gets the track-summary claim type.
    /// </summary>
    public const string Summary = "summary";

    /// <summary>
    ///     Gets the ordered-step claim type.
    /// </summary>
    public const string Step = "step";

    /// <summary>
    ///     Gets the purpose-card claim type.
    /// </summary>
    public const string Purpose = "purpose";

    /// <summary>
    ///     Gets the participant-relationship claim type.
    /// </summary>
    public const string Relationship = "relationship";
}
