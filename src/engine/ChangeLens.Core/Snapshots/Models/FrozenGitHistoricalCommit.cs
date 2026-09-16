namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents one bounded first-parent history commit and the paths it changed.
/// </summary>
/// <param name="Ordinal">The zero-based position in the inspected history window.</param>
/// <param name="Paths">The changed paths, including both sides of a rename. Cannot be <see langword="null" />.</param>
public sealed record FrozenGitHistoricalCommit(int Ordinal, IReadOnlySet<string> Paths);
