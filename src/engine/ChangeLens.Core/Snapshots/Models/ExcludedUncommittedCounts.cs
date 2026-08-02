namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents the distinct uncommitted lineage counts excluded from a snapshot manifest.
/// </summary>
/// <param name="Total">The number of distinct excluded uncommitted lineages.</param>
/// <param name="Staged">The number of distinct excluded lineages with staged changes.</param>
/// <param name="Unstaged">The number of distinct excluded lineages with unstaged changes.</param>
/// <param name="Untracked">The number of distinct excluded untracked lineages.</param>
/// <param name="Conflicted">The number of distinct excluded unmerged lineages.</param>
public sealed record ExcludedUncommittedCounts(int Total, int Staged, int Unstaged, int Untracked, int Conflicted);
