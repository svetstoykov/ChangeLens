namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents one strictly parsed committed comparison-file record.
/// </summary>
/// <param name="Path">The current repository-relative path. Cannot be <see langword="null" />.</param>
/// <param name="OriginalPath">
///     The previous repository-relative path for a rename, or <see langword="null" /> when no rename applies.
/// </param>
/// <param name="SourceMode">The six-digit merge-base tree-entry mode. Cannot be <see langword="null" />.</param>
/// <param name="TargetMode">The six-digit HEAD tree-entry mode. Cannot be <see langword="null" />.</param>
/// <param name="SourceObjectId">The merge-base object identifier. Cannot be <see langword="null" />.</param>
/// <param name="TargetObjectId">The HEAD object identifier. Cannot be <see langword="null" />.</param>
/// <param name="Status">The committed change category.</param>
internal sealed record GitComparisonFileRecord(
    string Path,
    string? OriginalPath,
    string SourceMode,
    string TargetMode,
    string SourceObjectId,
    string TargetObjectId,
    GitRawDiffStatus Status);
