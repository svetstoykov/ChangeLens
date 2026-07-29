using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.LocalState.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Stores repository history in the required SQLite local-state database.
/// </summary>
/// <remarks>
///     The Engine registers this implementation as scoped. It uses the request context and does not need to be thread-safe.
/// </remarks>
/// <param name="context">The scoped local-state context. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for repository-history persistence failures.</param>
public sealed class SqliteRepositoryHistoryStore(
    ChangeLensLocalStateDbContext context,
    ILogger<SqliteRepositoryHistoryStore> logger) : IRepositoryHistoryStore
{
    /// <inheritdoc />
    public async Task<Result<RepositoryHistoryEntry>> RecordOpenAsync(
        string canonicalPath,
        string canonicalPathKey,
        string displayName,
        long openedAtUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var repository = await context.Repositories.SingleOrDefaultAsync(
                entry => entry.CanonicalPathKey == canonicalPathKey,
                cancellationToken);
            if (repository is null)
            {
                repository = new RepositoryLocalState
                {
                    RepositoryId = Guid.NewGuid(),
                    CanonicalPath = canonicalPath,
                    CanonicalPathKey = canonicalPathKey,
                    DisplayName = displayName,
                    LastOpenedAtUnixMilliseconds = openedAtUnixMilliseconds,
                };
                context.Repositories.Add(repository);
            }
            else
            {
                repository.CanonicalPath = canonicalPath;
                repository.DisplayName = displayName;
                repository.LastOpenedAtUnixMilliseconds = openedAtUnixMilliseconds;
            }

            var applicationState = await context.ApplicationState.SingleAsync(
                entry => entry.SingletonId == 1,
                cancellationToken);
            applicationState.LastRepositoryId = repository.RepositoryId;
            await context.SaveChangesAsync(cancellationToken);

            var obsoleteRepositories = await context.Repositories
                .OrderByDescending(entry => entry.LastOpenedAtUnixMilliseconds)
                .ThenBy(entry => entry.RepositoryId)
                .Skip(LocalStateConstants.MaximumRecentRepositories)
                .ToListAsync(cancellationToken);
            context.Repositories.RemoveRange(obsoleteRepositories);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(ToEntry(repository));
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to record a repository history open.");
            return LocalStateFailure.Unavailable<RepositoryHistoryEntry>(exception);
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogWarning(
                exception,
                "Failed to record a repository history open: stored metadata is malformed.");
            return LocalStateFailure.Invalid<RepositoryHistoryEntry>();
        }
    }

    /// <inheritdoc />
    public async Task<Result<RepositoryHistoryEntry?>> GetLastAsync(CancellationToken cancellationToken)
    {
        try
        {
            var applicationState = await context.ApplicationState
                .AsNoTracking()
                .Include(entry => entry.LastRepository)
                .SingleAsync(entry => entry.SingletonId == 1, cancellationToken);
            return Result.Success<RepositoryHistoryEntry?>(
                applicationState.LastRepository is null ? null : ToEntry(applicationState.LastRepository));
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to read the last-opened repository from history.");
            return LocalStateFailure.Unavailable<RepositoryHistoryEntry?>(exception);
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogWarning(
                exception,
                "Failed to read the last-opened repository from history: stored metadata is malformed.");
            return LocalStateFailure.Invalid<RepositoryHistoryEntry?>();
        }
    }

    /// <inheritdoc />
    public async Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var lastRepositoryId = await context.ApplicationState
                .AsNoTracking()
                .Where(entry => entry.SingletonId == 1)
                .Select(entry => entry.LastRepositoryId)
                .SingleAsync(cancellationToken);
            var repositories = await context.Repositories
                .AsNoTracking()
                .OrderByDescending(entry => entry.LastOpenedAtUnixMilliseconds)
                .ThenBy(entry => entry.RepositoryId)
                .Take(LocalStateConstants.MaximumRecentRepositories)
                .Select(entry => ToEntry(entry))
                .ToListAsync(cancellationToken);
            return Result.Success(
                new RepositoryHistorySnapshot(lastRepositoryId, repositories.AsReadOnly()));
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to list recent repository history.");
            return LocalStateFailure.Unavailable<RepositoryHistorySnapshot>(exception);
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogWarning(exception, "Failed to list recent repository history: stored metadata is malformed.");
            return LocalStateFailure.Invalid<RepositoryHistorySnapshot>();
        }
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            var repository = await context.Repositories.SingleOrDefaultAsync(
                entry => entry.RepositoryId == repositoryId,
                cancellationToken);
            if (repository is not null)
            {
                context.Repositories.Remove(repository);
                await context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to remove repository {RepositoryId} from history.", repositoryId);
            return LocalStateFailure.Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetPreferredTargetAsync(
        string canonicalPathKey,
        string preferredTargetFullName,
        CancellationToken cancellationToken)
    {
        try
        {
            var repository = await context.Repositories.SingleOrDefaultAsync(
                entry => entry.CanonicalPathKey == canonicalPathKey,
                cancellationToken);
            if (repository is not null)
            {
                repository.PreferredTargetFullName = preferredTargetFullName;
                await context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to save a preferred comparison target.");
            return LocalStateFailure.Unavailable(exception);
        }
    }

    private static RepositoryHistoryEntry ToEntry(RepositoryLocalState repository) =>
        new(
            repository.RepositoryId,
            repository.CanonicalPath,
            repository.DisplayName,
            repository.LastOpenedAtUnixMilliseconds,
            repository.PreferredTargetFullName);
}
