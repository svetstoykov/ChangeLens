namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents one strictly parsed committed comparison-file record.
/// </summary>
/// <param name="Path">The current repository-relative path. Cannot be <see langword="null" />.</param>
/// <param name="OriginalPath">
///     The previous repository-relative path for a rename, or <see langword="null" /> when no rename applies.
/// </param>
internal sealed record GitComparisonFileRecord(
    string Path,
    string? OriginalPath);
