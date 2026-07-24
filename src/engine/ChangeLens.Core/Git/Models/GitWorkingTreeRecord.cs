namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents one strictly parsed working-tree file record.
/// </summary>
/// <param name="Path">The current repository-relative path. Cannot be <see langword="null" />.</param>
/// <param name="OriginalPath">
///     The previous repository-relative path for a rename, or <see langword="null" /> when no rename applies.
/// </param>
/// <param name="IsStaged">Whether the record includes a staged change.</param>
/// <param name="IsUnstaged">Whether the record includes an unstaged change.</param>
/// <param name="IsUntracked">Whether the record is untracked.</param>
/// <param name="IsConflicted">Whether the record is unmerged.</param>
/// <param name="IsIgnored">Whether the record is ignored.</param>
internal sealed record GitWorkingTreeRecord(
    string Path,
    string? OriginalPath,
    bool IsStaged,
    bool IsUnstaged,
    bool IsUntracked,
    bool IsConflicted,
    bool IsIgnored);
