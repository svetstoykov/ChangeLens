namespace ChangeLens.Core.Comparisons.Models;

/// <summary>
///     Represents one committed or working-tree file fact used for lineage composition.
/// </summary>
/// <param name="Path">The current repository-relative path. Cannot be <see langword="null" />.</param>
/// <param name="OriginalPath">
///     The previous repository-relative path for a rename, or <see langword="null" /> when no rename applies.
/// </param>
/// <param name="IsCommitted">Whether the fact originates from committed divergence.</param>
/// <param name="IsStaged">Whether the working-tree fact includes a staged change.</param>
/// <param name="IsUnstaged">Whether the working-tree fact includes an unstaged change.</param>
/// <param name="IsUntracked">Whether the working-tree fact is untracked.</param>
/// <param name="IsConflicted">Whether the working-tree fact is unmerged.</param>
public sealed record ComparisonFileRecord(
    string Path,
    string? OriginalPath,
    bool IsCommitted,
    bool IsStaged,
    bool IsUnstaged,
    bool IsUntracked,
    bool IsConflicted);
