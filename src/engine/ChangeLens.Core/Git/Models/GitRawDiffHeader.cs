namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents the validated fields of one raw diff header record.
/// </summary>
/// <param name="SourceMode">The six-digit merge-base tree-entry mode. Cannot be <see langword="null" />.</param>
/// <param name="TargetMode">The six-digit HEAD tree-entry mode. Cannot be <see langword="null" />.</param>
/// <param name="SourceObjectId">The merge-base object identifier. Cannot be <see langword="null" />.</param>
/// <param name="TargetObjectId">The HEAD object identifier. Cannot be <see langword="null" />.</param>
/// <param name="Status">The committed change category.</param>
internal readonly record struct GitRawDiffHeader(
    string SourceMode,
    string TargetMode,
    string SourceObjectId,
    string TargetObjectId,
    GitRawDiffStatus Status);
