using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.LocalState.Interfaces;

/// <summary>
///     Defines durable repository-history operations.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one request and do not need to be thread-safe.
/// </remarks>
public interface IRepositoryHistoryStore
{
    /// <summary>
    ///     Asynchronously records a successful explicit repository open.
    /// </summary>
    /// <param name="canonicalPath">The canonical absolute worktree path.</param>
    /// <param name="canonicalPathKey">The platform-normalized path identity key.</param>
    /// <param name="displayName">The last validated repository display name.</param>
    /// <param name="openedAtUnixMilliseconds">The open time in UTC Unix milliseconds.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the retained history entry.</returns>
    Task<Result<RepositoryHistoryEntry>> RecordOpenAsync(
        string canonicalPath,
        string canonicalPathKey,
        string displayName,
        long openedAtUnixMilliseconds,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously gets the repository selected for automatic restoration.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the entry, or <see langword="null" /> when none is selected.</returns>
    Task<Result<RepositoryHistoryEntry?>> GetLastAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously lists retained repository history.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the current history snapshot.</returns>
    Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously removes one history entry without changing repository files.
    /// </summary>
    /// <param name="repositoryId">The repository-history identifier.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result> RemoveAsync(Guid repositoryId, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously saves a preferred target only when the repository remains retained.
    /// </summary>
    /// <param name="canonicalPathKey">The platform-normalized path identity key.</param>
    /// <param name="preferredTargetFullName">The exact full Git ref.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result> SetPreferredTargetAsync(
        string canonicalPathKey,
        string preferredTargetFullName,
        CancellationToken cancellationToken);
}
