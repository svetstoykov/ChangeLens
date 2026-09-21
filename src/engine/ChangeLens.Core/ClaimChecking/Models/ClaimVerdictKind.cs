namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Defines the verdicts a checker can return for a claim.
/// </summary>
public enum ClaimVerdictKind
{
    /// <summary>
    ///     The supplied claim is supported by its quotes.
    /// </summary>
    Supported,

    /// <summary>
    ///     The supplied claim is not supported by its quotes.
    /// </summary>
    Unsupported,

    /// <summary>
    ///     A relationship kind is supported in place of the supplied kind.
    /// </summary>
    WrongKind,
}
