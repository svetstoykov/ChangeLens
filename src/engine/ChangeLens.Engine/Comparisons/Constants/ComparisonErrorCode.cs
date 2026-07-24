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
}
