namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents one retained repository-history entry in the protocol.
/// </summary>
/// <param name="RepositoryId">The ChangeLens repository identifier.</param>
/// <param name="Name">The last validated display name.</param>
/// <param name="CanonicalPath">The canonical absolute worktree path.</param>
/// <param name="LastOpenedAtUnixMilliseconds">The last successful explicit-open time.</param>
/// <param name="PreferredTarget">The saved full comparison ref, or <see langword="null" />.</param>
internal sealed record RecentRepositoryResult(
    Guid RepositoryId,
    string Name,
    string CanonicalPath,
    long LastOpenedAtUnixMilliseconds,
    string? PreferredTarget);
