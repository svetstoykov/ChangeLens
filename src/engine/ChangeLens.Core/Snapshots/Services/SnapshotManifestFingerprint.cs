using System.Globalization;
using System.Security.Cryptography;
using ChangeLens.Core.Hashing.Services;
using ChangeLens.Core.Snapshots.Constants;
using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Core.Snapshots.Services;

/// <summary>
///     Provides the canonical SHA-256 content hash for a committed snapshot manifest.
/// </summary>
internal static class SnapshotManifestFingerprint
{
    /// <summary>Creates the manifest hash over the header fields and the ordered entries.</summary>
    /// <param name="canonicalRepositoryPathKey">The normalized repository key. Cannot be <see langword="null" />.</param>
    /// <param name="targetReference">The exact comparison target reference. Cannot be <see langword="null" />.</param>
    /// <param name="targetRevision">The resolved target revision. Cannot be <see langword="null" />.</param>
    /// <param name="headRevision">The resolved HEAD revision. Cannot be <see langword="null" />.</param>
    /// <param name="mergeBaseRevision">The unique merge-base revision. Cannot be <see langword="null" />.</param>
    /// <param name="entries">The manifest entries in any order. Cannot be <see langword="null" />.</param>
    /// <returns>A deterministic 64-character lowercase SHA-256 hash.</returns>
    internal static string Create(
        string canonicalRepositoryPathKey,
        string targetReference,
        string targetRevision,
        string headRevision,
        string mergeBaseRevision,
        IReadOnlyList<SnapshotManifestEntry> entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        CanonicalFieldHasher.AppendField(hash, SnapshotManifestConstants.ManifestVersionMarker);
        CanonicalFieldHasher.AppendField(hash, "repository.canonical-path-key");
        CanonicalFieldHasher.AppendField(hash, canonicalRepositoryPathKey);
        CanonicalFieldHasher.AppendField(hash, "target");
        CanonicalFieldHasher.AppendField(hash, targetReference);
        CanonicalFieldHasher.AppendField(hash, targetRevision);
        CanonicalFieldHasher.AppendField(hash, "head-revision");
        CanonicalFieldHasher.AppendField(hash, headRevision);
        CanonicalFieldHasher.AppendField(hash, "merge-base-revision");
        CanonicalFieldHasher.AppendField(hash, mergeBaseRevision);

        var orderedEntries = Order(entries);
        CanonicalFieldHasher.AppendField(hash, "entry-count");
        CanonicalFieldHasher.AppendField(hash, orderedEntries.Length.ToString(CultureInfo.InvariantCulture));

        foreach (var entry in orderedEntries)
        {
            CanonicalFieldHasher.AppendField(hash, "entry");
            CanonicalFieldHasher.AppendField(hash, entry.Path);
            CanonicalFieldHasher.AppendNullableField(hash, "original-path", entry.OriginalPath);
            CanonicalFieldHasher.AppendField(hash, ToLiteral(entry.Category));
            CanonicalFieldHasher.AppendField(hash, entry.MergeBaseEntryMode);
            CanonicalFieldHasher.AppendField(hash, entry.HeadEntryMode);
            CanonicalFieldHasher.AppendField(hash, entry.MergeBaseObjectId);
            CanonicalFieldHasher.AppendField(hash, entry.HeadObjectId);
        }

        return CanonicalFieldHasher.Complete(hash);
    }

    /// <summary>Maps one category to its stable lowercase hash and column literal.</summary>
    /// <param name="category">The category to map.</param>
    /// <returns>The stable literal.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="category" /> is not a defined member.</exception>
    internal static string ToLiteral(SnapshotChangeCategory category) => category switch
    {
        SnapshotChangeCategory.Added => SnapshotManifestConstants.AddedCategory,
        SnapshotChangeCategory.Modified => SnapshotManifestConstants.ModifiedCategory,
        SnapshotChangeCategory.Deleted => SnapshotManifestConstants.DeletedCategory,
        SnapshotChangeCategory.Renamed => SnapshotManifestConstants.RenamedCategory,
        SnapshotChangeCategory.TypeChanged => SnapshotManifestConstants.TypeChangedCategory,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "The snapshot change category is not supported."),
    };

    /// <summary>Orders entries exactly as the comparison fingerprint orders working-tree records.</summary>
    /// <param name="entries">The manifest entries. Cannot be <see langword="null" />.</param>
    /// <returns>The canonically ordered entries.</returns>
    internal static SnapshotManifestEntry[] Order(IReadOnlyList<SnapshotManifestEntry> entries) =>
        entries
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.OriginalPath is null ? 0 : 1)
            .ThenBy(entry => entry.OriginalPath, StringComparer.Ordinal)
            .ToArray();
}
