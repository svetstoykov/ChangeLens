namespace ChangeLens.Core.ClaimChecking.Constants;

/// <summary>
///     Defines the quote Role values accepted by the checker port.
/// </summary>
public static class CheckerQuoteRole
{
    /// <summary>
    ///     Gets the source-participant quote role.
    /// </summary>
    public const string From = "from";

    /// <summary>
    ///     Gets the target-participant quote role.
    /// </summary>
    public const string To = "to";

    /// <summary>
    ///     Gets the non-endpoint quote role on a relationship claim.
    /// </summary>
    public const string Context = "context";
}
