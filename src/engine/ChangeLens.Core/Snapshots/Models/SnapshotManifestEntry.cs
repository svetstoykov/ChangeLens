namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents one committed path in a snapshot manifest with its exact Git facts.
/// </summary>
/// <param name="Path">The current repository-relative path. Cannot be <see langword="null" />.</param>
/// <param name="OriginalPath">
///     The previous repository-relative path for a rename, or <see langword="null" /> when no rename applies.
/// </param>
/// <param name="Category">The committed change category.</param>
/// <param name="MergeBaseEntryMode">The six-digit merge-base tree-entry mode. Cannot be <see langword="null" />.</param>
/// <param name="HeadEntryMode">The six-digit HEAD tree-entry mode. Cannot be <see langword="null" />.</param>
/// <param name="MergeBaseObjectId">The merge-base object identifier. Cannot be <see langword="null" />.</param>
/// <param name="HeadObjectId">The HEAD object identifier. Cannot be <see langword="null" />.</param>
public sealed record SnapshotManifestEntry(
    string Path,
    string? OriginalPath,
    SnapshotChangeCategory Category,
    string MergeBaseEntryMode,
    string HeadEntryMode,
    string MergeBaseObjectId,
    string HeadObjectId);
