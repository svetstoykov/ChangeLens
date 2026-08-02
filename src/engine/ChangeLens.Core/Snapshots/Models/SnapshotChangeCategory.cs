namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Defines the committed change categories a snapshot manifest entry can carry.
/// </summary>
public enum SnapshotChangeCategory
{
    /// <summary>The path exists only on the HEAD side.</summary>
    Added,

    /// <summary>The path exists on both sides with a different object or mode of the same file type.</summary>
    Modified,

    /// <summary>The path exists only on the merge-base side.</summary>
    Deleted,

    /// <summary>The path moved from <see cref="SnapshotManifestEntry.OriginalPath" />.</summary>
    Renamed,

    /// <summary>The path exists on both sides with a different Git file type.</summary>
    TypeChanged,
}
