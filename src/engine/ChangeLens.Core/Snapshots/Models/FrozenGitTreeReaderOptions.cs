namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents configurable bounds for frozen Git tree and history reads.
/// </summary>
public sealed class FrozenGitTreeReaderOptions
{
    /// <summary>
    ///     Gets or sets the largest blob byte count read as text. The default is 64,000 bytes.
    /// </summary>
    public int MaximumBlobBytes { get; set; } = 64_000;

    /// <summary>
    ///     Gets or sets the maximum number of files returned by a tree listing. The default is 20,000 files.
    /// </summary>
    public int MaximumTreeFiles { get; set; } = 20_000;

    /// <summary>
    ///     Gets or sets the maximum number of first-parent commits inspected. The default is 200 commits.
    /// </summary>
    public int MaximumHistoryCommits { get; set; } = 200;

    /// <summary>
    ///     Gets or sets the maximum number of paths accepted from one history commit. The default is 200 paths.
    /// </summary>
    public int MaximumHistoryPathsPerCommit { get; set; } = 200;

    /// <summary>
    ///     Gets or sets the time allowed for one Git command. The default is 15 seconds.
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(15);
}
