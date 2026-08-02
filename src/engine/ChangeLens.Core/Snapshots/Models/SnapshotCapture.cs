namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents one completed committed-content snapshot capture.
/// </summary>
/// <param name="Manifest">The hashed, bounded committed comparison manifest. Cannot be <see langword="null" />.</param>
/// <param name="ExcludedUncommittedCounts">
///     The uncommitted lineage counts excluded from the manifest. Cannot be <see langword="null" />.
/// </param>
public sealed record SnapshotCapture(SnapshotManifest Manifest, ExcludedUncommittedCounts ExcludedUncommittedCounts);
