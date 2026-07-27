namespace ChangeLens.Core.Git.Constants;

/// <summary>
///     Provides the fixed resource limits for remote baseline detection and refresh.
/// </summary>
internal static class GitRemoteConstants
{
    /// <summary>
    ///     The total time allowed for one remote baseline check, which transfers no objects.
    /// </summary>
    internal static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     The total time allowed for one remote baseline refresh, which fetches exactly one ref.
    /// </summary>
    internal static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    ///     The maximum number of UTF-8 bytes accepted from either output stream of a remote command.
    /// </summary>
    internal const int MaximumStreamBytes = 64 * 1024;
}
