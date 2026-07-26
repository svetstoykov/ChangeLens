using ChangeLens.Core.Git.Services;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.Repositories.Services;

/// <summary>
///     Coordinates repository inspection with durable repository history.
/// </summary>
/// <remarks>
///     The Engine registers this stateless service as a singleton. Its collaborators are singleton-safe.
/// </remarks>
/// <param name="repositoryInspector">The read-only repository inspector.</param>
/// <param name="historyStore">The durable repository-history store.</param>
/// <param name="pathKeyProvider">The platform-specific canonical-path key provider.</param>
/// <param name="timeProvider">The time source used for explicit-open timestamps.</param>
internal sealed class RepositoryHistoryService(
    GitRepositoryInspector repositoryInspector,
    IRepositoryHistoryStore historyStore,
    ICanonicalRepositoryPathKeyProvider pathKeyProvider,
    TimeProvider timeProvider)
{
    /// <summary>
    ///     Asynchronously inspects and records an explicitly opened repository.
    /// </summary>
    /// <param name="path">The selected path.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the opened repository and retained metadata.</returns>
    internal async Task<Result<OpenedRepository>> OpenAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var inspectionResult = await repositoryInspector.InspectAsync(path, cancellationToken);
        if (inspectionResult.IsFailure)
        {
            return Result.ErrorFromResult<OpenedRepository>(inspectionResult);
        }

        var repository = inspectionResult.Data!;
        var recordResult = await historyStore.RecordOpenAsync(
            repository.CanonicalPath,
            pathKeyProvider.CreateKey(repository.CanonicalPath),
            repository.Name,
            timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            cancellationToken);
        if (recordResult.IsFailure)
        {
            return Result.ErrorFromResult<OpenedRepository>(recordResult);
        }

        return Result.Success(new OpenedRepository(recordResult.Data!, repository));
    }

    /// <summary>
    ///     Asynchronously restores and revalidates the last selected repository.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the optional restored repository.</returns>
    internal async Task<Result<RepositoryRestoration>> RestoreLastAsync(
        CancellationToken cancellationToken)
    {
        var lastResult = await historyStore.GetLastAsync(cancellationToken);
        if (lastResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryRestoration>(lastResult);
        }

        var entry = lastResult.Data;
        if (entry is null)
        {
            return Result.Success(new RepositoryRestoration(null, null));
        }

        var inspectionResult = await repositoryInspector.InspectAsync(
            entry.CanonicalPath,
            cancellationToken);
        if (inspectionResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryRestoration>(inspectionResult);
        }

        return Result.Success(new RepositoryRestoration(entry, inspectionResult.Data!));
    }

    /// <summary>
    ///     Asynchronously lists recent repositories without revalidating them.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the history snapshot.</returns>
    internal Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(
        CancellationToken cancellationToken) =>
        historyStore.ListRecentAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously removes one repository-history entry.
    /// </summary>
    /// <param name="repositoryId">The repository-history identifier.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task<Result> RemoveRecentAsync(
        Guid repositoryId,
        CancellationToken cancellationToken) =>
        historyStore.RemoveAsync(repositoryId, cancellationToken);

    /// <summary>
    ///     Asynchronously saves an exact preferred target for a retained repository.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository path.</param>
    /// <param name="preferredTargetFullName">The exact full Git ref.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task<Result> SavePreferredTargetAsync(
        string canonicalPath,
        string preferredTargetFullName,
        CancellationToken cancellationToken) =>
        historyStore.SetPreferredTargetAsync(
            pathKeyProvider.CreateKey(canonicalPath),
            preferredTargetFullName,
            cancellationToken);
}
