namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents the bounded first-parent history collected from the captured merge-base revision.
/// </summary>
/// <param name="Commits">The accepted history commits. Cannot be <see langword="null" />.</param>
/// <param name="CommitsInspected">The number of commits considered before path filtering.</param>
/// <param name="OversizedCommitsSkipped">The number of commits ignored for exceeding the path cap.</param>
/// <param name="WasTruncated">Whether more commits were available beyond the configured history cap.</param>
public sealed record FrozenGitHistoryScan(
    IReadOnlyList<FrozenGitHistoricalCommit> Commits,
    int CommitsInspected,
    int OversizedCommitsSkipped,
    bool WasTruncated);
