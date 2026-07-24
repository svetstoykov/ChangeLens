namespace ChangeLens.Core.Comparisons.Models;

/// <summary>
///     Defines whether a prepared comparison can be presented.
/// </summary>
public enum ComparisonReadiness
{
    /// <summary>
    ///     A comparison with one or more changed files and no conflicts.
    /// </summary>
    Ready,

    /// <summary>
    ///     A comparison with no changed files.
    /// </summary>
    Empty,

    /// <summary>
    ///     A comparison whose working tree contains conflicts.
    /// </summary>
    Conflicts,
}
