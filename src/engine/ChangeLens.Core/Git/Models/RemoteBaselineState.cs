namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Defines whether a cached remote-tracking reference still matches the server's branch.
/// </summary>
public enum RemoteBaselineState
{
    /// <summary>
    ///     The cached remote-tracking reference matches the server's branch.
    /// </summary>
    Current,

    /// <summary>
    ///     The server's branch has moved since the cached remote-tracking reference was last written.
    /// </summary>
    Moved,

    /// <summary>
    ///     The repository has no configured remote for the selected target.
    /// </summary>
    NoRemote,
}
