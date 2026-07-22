namespace ChangeLens.Core.Git.Constants;

/// <summary>
///     Provides the fixed resource limits for Git repository inspection.
/// </summary>
internal static class GitInspectionConstants
{
    /// <summary>
    ///     The maximum number of UTF-8 bytes accepted from either Git output stream.
    /// </summary>
    internal const int MaximumStreamBytes = 64 * 1024;

    /// <summary>
    ///     The total time allowed for one Git repository inspection.
    /// </summary>
    internal static readonly TimeSpan InspectionTimeout = TimeSpan.FromSeconds(15);
}
