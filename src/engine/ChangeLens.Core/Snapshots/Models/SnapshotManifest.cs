namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents the hashed, bounded committed comparison captured for one accepted analysis run.
/// </summary>
/// <param name="SnapshotId">The identifier of this particular capture occurrence. Cannot be <see langword="null" />.</param>
/// <param name="ManifestHash">The deterministic content hash over the header and ordered entries. Cannot be <see langword="null" />.</param>
/// <param name="CanonicalRepositoryPathKey">The normalized repository key. Cannot be <see langword="null" />.</param>
/// <param name="TargetReference">The exact comparison target reference. Cannot be <see langword="null" />.</param>
/// <param name="TargetRevision">The resolved target revision. Cannot be <see langword="null" />.</param>
/// <param name="HeadRevision">The resolved HEAD revision. Cannot be <see langword="null" />.</param>
/// <param name="MergeBaseRevision">The unique merge-base revision. Cannot be <see langword="null" />.</param>
/// <param name="Entries">The captured manifest entries. Cannot be <see langword="null" />.</param>
public sealed record SnapshotManifest(
    Guid SnapshotId,
    string ManifestHash,
    string CanonicalRepositoryPathKey,
    string TargetReference,
    string TargetRevision,
    string HeadRevision,
    string MergeBaseRevision,
    IReadOnlyList<SnapshotManifestEntry> Entries);
