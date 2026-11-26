namespace ChangeLens.Core.Analysis.ClaimChecking.Constants;

/// <summary>
///     Provides stable error codes for deterministic claim checking.
/// </summary>
public static class ClaimCheckingErrorCode
{
    /// <summary>
    ///     Identifies a claim-checking request made when no checker adapter is registered.
    /// </summary>
    public const string Unavailable = "claimChecking.unavailable";
}
