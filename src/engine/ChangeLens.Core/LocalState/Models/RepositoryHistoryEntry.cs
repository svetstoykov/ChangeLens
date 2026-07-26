namespace ChangeLens.Core.LocalState.Models;

/// <summary>
///     Represents one retained repository-history entry.
/// </summary>
/// <param name="RepositoryId">The ChangeLens-generated repository identifier.</param>
/// <param name="CanonicalPath">The canonical absolute worktree path.</param>
/// <param name="DisplayName">The last validated repository display name.</param>
/// <param name="LastOpenedAtUnixMilliseconds">The last successful explicit-open time in UTC Unix milliseconds.</param>
/// <param name="PreferredTargetFullName">The saved full comparison ref, or <see langword="null" />.</param>
public sealed record RepositoryHistoryEntry(
    Guid RepositoryId,
    string CanonicalPath,
    string DisplayName,
    long LastOpenedAtUnixMilliseconds,
    string? PreferredTargetFullName);
