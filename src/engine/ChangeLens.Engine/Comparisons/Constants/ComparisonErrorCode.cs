namespace ChangeLens.Engine.Comparisons.Constants;

/// <summary>
///     Provides stable Engine-originated comparison error codes.
/// </summary>
internal static class ComparisonErrorCode
{
    /// <summary>
    ///     Identifies a complete comparison response that exceeds the supported protocol limit.
    /// </summary>
    internal const string TooLarge = "comparison.tooLarge";

    /// <summary>
    ///     Identifies a comparison freshness state that has no approved protocol representation.
    /// </summary>
    internal const string UnmappedFreshnessState = "comparison.unmappedFreshnessState";

    /// <summary>
    ///     Identifies a remote-baseline state that has no approved protocol representation.
    /// </summary>
    internal const string UnmappedRemoteBaselineState = "comparison.unmappedRemoteBaselineState";
}
