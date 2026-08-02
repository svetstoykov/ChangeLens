using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.Snapshots.Persistence.Entities;

namespace ChangeLens.Infrastructure.Snapshots.Services;

/// <summary>
///     Adds snapshot manifest entry rows to a shared context inside the caller's capture transaction.
/// </summary>
internal static class SnapshotManifestEntryWriter
{
    /// <summary>Adds one row per manifest entry without saving.</summary>
    /// <param name="context">The scoped local-state context. Cannot be <see langword="null" />.</param>
    /// <param name="runId">The owning run identifier.</param>
    /// <param name="entries">The manifest entries. Cannot be <see langword="null" />.</param>
    internal static void Add(ChangeLensLocalStateDbContext context, Guid runId, IReadOnlyList<SnapshotManifestEntry> entries)
    {
        foreach (var entry in entries)
        {
            context.SnapshotManifestEntries.Add(new SnapshotManifestEntryEntity
            {
                RunId = runId,
                Path = entry.Path,
                OriginalPath = entry.OriginalPath,
                Category = entry.Category,
                MergeBaseEntryMode = entry.MergeBaseEntryMode,
                HeadEntryMode = entry.HeadEntryMode,
                MergeBaseObjectId = entry.MergeBaseObjectId,
                HeadObjectId = entry.HeadObjectId,
            });
        }
    }
}
