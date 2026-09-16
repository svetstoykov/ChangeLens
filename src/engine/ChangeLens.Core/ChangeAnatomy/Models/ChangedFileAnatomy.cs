using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Core.ChangeAnatomy.Models;

/// <summary>
///     Represents the keys or explicit skip produced for one captured manifest entry.
/// </summary>
/// <param name="Path">The current repository-relative path. Cannot be <see langword="null" />.</param>
/// <param name="OriginalPath">The previous path for a rename, or <see langword="null" />.</param>
/// <param name="Category">The committed change category.</param>
/// <param name="Keys">The keys found on touched lines and paths. Cannot be <see langword="null" />.</param>
/// <param name="Truncated">Whether the per-file key cap omitted one or more distinct keys.</param>
/// <param name="SkipReason">A human-readable skip reason, or <see langword="null" /> when analyzed.</param>
public sealed record ChangedFileAnatomy(
    string Path,
    string? OriginalPath,
    SnapshotChangeCategory Category,
    IReadOnlyList<ChangeAnatomyKey> Keys,
    bool Truncated,
    string? SkipReason)
{
    /// <summary>
    ///     Gets a value indicating whether this entry was analyzed rather than skipped.
    /// </summary>
    public bool IsAnalyzed => this.SkipReason is null;

    /// <summary>
    ///     Gets a value indicating whether this analyzed entry yielded no keys.
    /// </summary>
    public bool HasZeroKeys => this.IsAnalyzed && this.Keys.Count == 0;
}
