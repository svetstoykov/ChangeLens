using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;

namespace ChangeLens.Infrastructure.Snapshots.Persistence.Entities;

/// <summary>
///     Represents one persisted committed path from a run's captured snapshot manifest.
/// </summary>
internal sealed class SnapshotManifestEntryEntity
{
    /// <summary>
    ///     Gets or sets the identifier of the owning run.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    ///     Gets or sets the current repository-relative path.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    ///     Gets or sets the previous repository-relative path for a rename, or <see langword="null" /> when no
    ///     rename applies.
    /// </summary>
    public string? OriginalPath { get; set; }

    /// <summary>
    ///     Gets or sets the committed change category.
    /// </summary>
    public SnapshotChangeCategory Category { get; set; }

    /// <summary>
    ///     Gets or sets the six-digit merge-base tree-entry mode.
    /// </summary>
    public required string MergeBaseEntryMode { get; set; }

    /// <summary>
    ///     Gets or sets the six-digit HEAD tree-entry mode.
    /// </summary>
    public required string HeadEntryMode { get; set; }

    /// <summary>
    ///     Gets or sets the merge-base object identifier.
    /// </summary>
    public required string MergeBaseObjectId { get; set; }

    /// <summary>
    ///     Gets or sets the HEAD object identifier.
    /// </summary>
    public required string HeadObjectId { get; set; }

    /// <summary>
    ///     Gets or sets the owning run.
    /// </summary>
    public AnalysisRunEntity? Run { get; set; }
}
