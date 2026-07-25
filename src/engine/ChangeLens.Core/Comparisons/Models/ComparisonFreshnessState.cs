namespace ChangeLens.Core.Comparisons.Models;

/// <summary>
///     Defines whether prepared comparison facts still match the repository.
/// </summary>
public enum ComparisonFreshnessState
{
    /// <summary>
    ///     The prepared facts still match the repository.
    /// </summary>
    Current,

    /// <summary>
    ///     One or more prepared facts no longer match the repository.
    /// </summary>
    Stale,
}
