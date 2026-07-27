using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Interfaces;
using ChangeLens.Infrastructure.LocalState.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Stores repository history in the required SQLite local-state database.
/// </summary>
/// <remarks>
///     The Engine registers this stateless implementation as a singleton. Each operation uses its own Entity Framework context.
/// </remarks>
/// <param name="database">The required SQLite local-state database.</param>
/// <param name="logger">The logger for repository-history persistence failures.</param>
public sealed class SqliteRepositoryHistoryStore(
    ILocalStateDatabase database,
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
            await using var context = await database.CreateContextAsync(cancellationToken);
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
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to record a repository history open.");
            return SqliteLocalStateDatabase.Unavailable<RepositoryHistoryEntry>(exception);
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsMalformedDataFailure(exception))
        {
            logger.LogWarning(
                exception,
                "Failed to record a repository history open: stored metadata is malformed.");
            return SqliteLocalStateDatabase.Invalid<RepositoryHistoryEntry>();
        }
    }

    /// <inheritdoc />
    public async Task<Result<RepositoryHistoryEntry?>> GetLastAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await database.CreateContextAsync(cancellationToken);
            var applicationState = await context.ApplicationState
                .Include(entry => entry.LastRepository)
                .SingleAsync(entry => entry.SingletonId == 1, cancellationToken);
            return Result.Success<RepositoryHistoryEntry?>(
                applicationState.LastRepository is null ? null : ToEntry(applicationState.LastRepository));
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to read the last-opened repository from history.");
            return SqliteLocalStateDatabase.Unavailable<RepositoryHistoryEntry?>(exception);
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsMalformedDataFailure(exception))
        {
            logger.LogWarning(
                exception,
                "Failed to read the last-opened repository from history: stored metadata is malformed.");
            return SqliteLocalStateDatabase.Invalid<RepositoryHistoryEntry?>();
        }
    }

    /// <inheritdoc />
    public async Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await database.CreateContextAsync(cancellationToken);
            var lastRepositoryId = await context.ApplicationState
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
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to list recent repository history.");
            return SqliteLocalStateDatabase.Unavailable<RepositoryHistorySnapshot>(exception);
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsMalformedDataFailure(exception))
        {
            logger.LogWarning(exception, "Failed to list recent repository history: stored metadata is malformed.");
            return SqliteLocalStateDatabase.Invalid<RepositoryHistorySnapshot>();
        }
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await database.CreateContextAsync(cancellationToken);
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
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to remove repository {RepositoryId} from history.", repositoryId);
            return SqliteLocalStateDatabase.Unavailable(exception);
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
            await using var context = await database.CreateContextAsync(cancellationToken);
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
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to save a preferred comparison target.");
            return SqliteLocalStateDatabase.Unavailable(exception);
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
