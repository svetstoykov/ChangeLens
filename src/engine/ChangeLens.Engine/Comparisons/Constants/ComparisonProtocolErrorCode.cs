namespace ChangeLens.Engine.Comparisons.Constants;

/// <summary>
///     Provides stable Engine-originated comparison protocol error codes.
/// </summary>
internal static class ComparisonProtocolErrorCode
{
    /// <summary>
    ///     Identifies a comparison freshness state that has no approved protocol representation.
    /// </summary>
    internal const string UnmappedFreshnessState = "comparison.unmappedFreshnessState";

    /// <summary>
    ///     Identifies a remote-baseline state that has no approved protocol representation.
    /// </summary>
    internal const string UnmappedRemoteBaselineState = "comparison.unmappedRemoteBaselineState";
}
