namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents a comparison whose working tree contains conflicts.
/// </summary>
/// <param name="ConflictedFileCount">The number of distinct unmerged file lineages.</param>
internal sealed record ConflictsComparisonReadinessResult(
    int ConflictedFileCount) : ComparisonReadinessResult;
