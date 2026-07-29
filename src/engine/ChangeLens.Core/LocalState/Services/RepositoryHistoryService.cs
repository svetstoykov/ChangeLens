using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Core.LocalState.Services;

/// <summary>
///     Coordinates repository inspection with durable repository history.
/// </summary>
/// <remarks>
///     The Engine registers this service as scoped. It serves one request and does not need to be thread-safe.
/// </remarks>
/// <param name="repositoryInspector">The read-only repository inspector.</param>
/// <param name="historyStore">The durable repository-history store.</param>
/// <param name="pathKeyProvider">The platform-specific canonical-path key provider.</param>
/// <param name="timeProvider">The time source used for explicit-open timestamps.</param>
/// <param name="logger">The logger for repository history outcomes.</param>
public sealed class RepositoryHistoryService(
    IGitRepositoryInspector repositoryInspector,
    IRepositoryHistoryStore historyStore,
    ICanonicalRepositoryPathKeyProvider pathKeyProvider,
    TimeProvider timeProvider,
    ILogger<RepositoryHistoryService> logger) : IRepositoryHistoryService
{
    /// <inheritdoc />
    public async Task<Result<OpenedRepository>> OpenAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var inspectionResult = await repositoryInspector.InspectAsync(path, cancellationToken);
        if (inspectionResult.IsFailure)
        {
            logger.LogWarning("Failed to open the selected repository: inspection did not produce a repository.");
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

        logger.LogInformation("Opened repository {RepositoryName}.", repository.Name);
        return Result.Success(new OpenedRepository(recordResult.Data!, repository));
    }

    /// <inheritdoc />
    public async Task<Result<RepositoryRestoration>> RestoreLastAsync(
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
            logger.LogDebug("No repository history entry available to restore.");
            return Result.Success(new RepositoryRestoration(null, null));
        }

        var inspectionResult = await repositoryInspector.InspectAsync(
            entry.CanonicalPath,
            cancellationToken);
        if (inspectionResult.IsFailure)
        {
            logger.LogWarning("Failed to restore the last repository: it is no longer inspectable.");
            return Result.ErrorFromResult<RepositoryRestoration>(inspectionResult);
        }

        logger.LogInformation("Restored last repository {RepositoryName}.", entry.DisplayName);
        return Result.Success(new RepositoryRestoration(entry, inspectionResult.Data!));
    }

    /// <inheritdoc />
    public Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(
        CancellationToken cancellationToken) =>
        historyStore.ListRecentAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Result> RemoveRecentAsync(
        Guid repositoryId,
        CancellationToken cancellationToken) =>
        historyStore.RemoveAsync(repositoryId, cancellationToken);

    /// <inheritdoc />
    public Task<Result> SavePreferredTargetAsync(
        string canonicalPath,
        string preferredTargetFullName,
        CancellationToken cancellationToken) =>
        historyStore.SetPreferredTargetAsync(
            pathKeyProvider.CreateKey(canonicalPath),
            preferredTargetFullName,
            cancellationToken);
}
