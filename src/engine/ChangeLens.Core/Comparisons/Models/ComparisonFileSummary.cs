namespace ChangeLens.Core.Comparisons.Models;

/// <summary>
///     Represents distinct comparison-file lineage and overlapping working-tree category counts.
/// </summary>
/// <param name="ChangedFileTotal">The number of distinct changed file lineages.</param>
/// <param name="UncommittedFileTotal">The number of distinct uncommitted file lineages.</param>
/// <param name="StagedFileCount">The number of distinct lineages with staged changes.</param>
/// <param name="UnstagedFileCount">The number of distinct lineages with unstaged changes.</param>
/// <param name="UntrackedFileCount">The number of distinct untracked lineages.</param>
/// <param name="ConflictedFileCount">The number of distinct unmerged lineages.</param>
public sealed record ComparisonFileSummary(
    int ChangedFileTotal,
    int UncommittedFileTotal,
    int StagedFileCount,
    int UnstagedFileCount,
    int UntrackedFileCount,
    int ConflictedFileCount);
