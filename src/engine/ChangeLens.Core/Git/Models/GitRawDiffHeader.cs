namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents the validated fields of one raw diff header record.
/// </summary>
/// <param name="SourceMode">The six-digit merge-base tree-entry mode. Cannot be <see langword="null" />.</param>
/// <param name="TargetMode">The six-digit HEAD tree-entry mode. Cannot be <see langword="null" />.</param>
/// <param name="SourceObjectId">The merge-base object identifier. Cannot be <see langword="null" />.</param>
/// <param name="TargetObjectId">The HEAD object identifier. Cannot be <see langword="null" />.</param>
/// <param name="Status">The raw status character: <c>A</c>, <c>D</c>, <c>M</c>, <c>T</c>, or <c>R</c>.</param>
internal readonly record struct GitRawDiffHeader(
    string SourceMode,
    string TargetMode,
    string SourceObjectId,
    string TargetObjectId,
    char Status);
