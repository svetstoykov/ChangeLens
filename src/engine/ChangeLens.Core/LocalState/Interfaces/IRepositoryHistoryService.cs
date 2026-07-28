using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.LocalState.Interfaces;

/// <summary>
///     Defines repository-history use cases.
/// </summary>
public interface IRepositoryHistoryService
{
    /// <summary>
    ///     Asynchronously inspects and records an explicitly opened repository.
    /// </summary>
    /// <param name="path">The selected path.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the opened repository and retained metadata.</returns>
    Task<Result<OpenedRepository>> OpenAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously restores and revalidates the last selected repository.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the optional restored repository.</returns>
    Task<Result<RepositoryRestoration>> RestoreLastAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously lists recent repositories without revalidating them.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the history snapshot.</returns>
    Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously removes one repository-history entry.
    /// </summary>
    /// <param name="repositoryId">The repository-history identifier.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result> RemoveRecentAsync(Guid repositoryId, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously saves an exact preferred target for a retained repository.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository path.</param>
    /// <param name="preferredTargetFullName">The exact full Git ref.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result> SavePreferredTargetAsync(
        string canonicalPath,
        string preferredTargetFullName,
        CancellationToken cancellationToken);
}
