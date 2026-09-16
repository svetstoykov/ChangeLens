namespace ChangeLens.Core.Snapshots.Constants;

/// <summary>
///     Provides configuration keys for bounded frozen Git reads.
/// </summary>
public static class FrozenGitTreeReaderConfigurationConstants
{
    /// <summary>
    ///     The configuration section containing frozen Git tree-reader settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Snapshots:FrozenGitTree";

    /// <summary>
    ///     The maximum blob byte-count configuration key.
    /// </summary>
    public const string MaximumBlobBytesKey = SectionKey + ":MaximumBlobBytes";

    /// <summary>
    ///     The maximum tree-file configuration key.
    /// </summary>
    public const string MaximumTreeFilesKey = SectionKey + ":MaximumTreeFiles";

    /// <summary>
    ///     The maximum history-commit configuration key.
    /// </summary>
    public const string MaximumHistoryCommitsKey = SectionKey + ":MaximumHistoryCommits";

    /// <summary>
    ///     The maximum history-paths-per-commit configuration key.
    /// </summary>
    public const string MaximumHistoryPathsPerCommitKey = SectionKey + ":MaximumHistoryPathsPerCommit";

    /// <summary>
    ///     The Git command timeout configuration key.
    /// </summary>
    public const string CommandTimeoutKey = SectionKey + ":CommandTimeout";
}
