namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Defines why a captured blob was not returned as text.
/// </summary>
public enum FrozenGitBlobSkipReason
{
    /// <summary>
    ///     No skip was applied.
    /// </summary>
    None,

    /// <summary>
    ///     The blob was detected as binary content.
    /// </summary>
    Binary,

    /// <summary>
    ///     The blob exceeded the configured byte bound.
    /// </summary>
    TooLarge,
}
