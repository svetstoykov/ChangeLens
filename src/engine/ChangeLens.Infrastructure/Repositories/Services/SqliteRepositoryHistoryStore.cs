using System.Diagnostics;
using ChangeLens.Core.Repositories.Interfaces;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Helpers;
using ChangeLens.Infrastructure.LocalState.Persistence.Entities;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.Repositories.Services;

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
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var repository = await context.Repositories.SingleOrDefaultAsync(entry => entry.CanonicalPathKey == canonicalPathKey, cancellationToken);
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

            var applicationState = await context.ApplicationState.SingleOrDefaultAsync(entry => entry.SingletonId == 1, cancellationToken);
            if (applicationState is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    "Failed to record a repository history open: singleton application state row is missing after {ElapsedMilliseconds:0.000} ms.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return LocalStateFailure.Invalid<RepositoryHistoryEntry>();
            }

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

            logger.LogDebug(
                "Recorded repository open for {RepositoryId} ({DisplayName}) and pruned {PrunedCount} obsolete entry(ies).",
                repository.RepositoryId, displayName, obsoleteRepositories.Count);
            logger.LogInformation(
                "Recorded repository history open for {RepositoryId} in {ElapsedMilliseconds:0.000} ms.",
                repository.RepositoryId, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Success(ToEntry(repository));
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to record a repository history open after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Unavailable<RepositoryHistoryEntry>();
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to record a repository history open: stored metadata is malformed after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Invalid<RepositoryHistoryEntry>();
        }
    }

    /// <inheritdoc />
    public async Task<Result<RepositoryHistoryEntry?>> GetLastAsync(CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var applicationState = await context.ApplicationState
                .AsNoTracking()
                .Include(entry => entry.LastRepository)
                .SingleOrDefaultAsync(entry => entry.SingletonId == 1, cancellationToken);

            if (applicationState is null)
            {
                logger.LogWarning(
                    "Failed to read last-opened repository from history: singleton application state row is missing after "
                    + "{ElapsedMilliseconds:0.000} ms.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return LocalStateFailure.Invalid<RepositoryHistoryEntry?>();
            }

            var entry = applicationState.LastRepository is null ? null : ToEntry(applicationState.LastRepository);
            logger.LogDebug("Queried last-opened repository from history; matched {RepositoryId}.", entry?.RepositoryId);
            logger.LogInformation(
                "Read last-opened repository from history (found: {Found}) in {ElapsedMilliseconds:0.000} ms.",
                entry is not null, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Success<RepositoryHistoryEntry?>(entry);
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to read the last-opened repository from history after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Unavailable<RepositoryHistoryEntry?>();
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to read the last-opened repository from history: stored metadata is malformed after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Invalid<RepositoryHistoryEntry?>();
        }
    }

    /// <inheritdoc />
    public async Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var applicationState = await context.ApplicationState
                .AsNoTracking()
                .Where(entry => entry.SingletonId == 1)
                .Select(entry => new { entry.LastRepositoryId })
                .SingleOrDefaultAsync(cancellationToken);

            if (applicationState is null)
            {
                logger.LogWarning(
                    "Failed to list recent repository history: singleton application state row is missing after {ElapsedMilliseconds:0.000} ms.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return LocalStateFailure.Invalid<RepositoryHistorySnapshot>();
            }

            var repositories = await context.Repositories
                .AsNoTracking()
                .OrderByDescending(entry => entry.LastOpenedAtUnixMilliseconds)
                .ThenBy(entry => entry.RepositoryId)
                .Take(LocalStateConstants.MaximumRecentRepositories)
                .Select(entry => ToEntry(entry))
                .ToListAsync(cancellationToken);

            logger.LogDebug(
                "Queried recent repository history; returned {Count} item(s) with last repository {LastRepositoryId}.",
                repositories.Count, applicationState.LastRepositoryId);
            logger.LogInformation(
                "Listed {Count} recent repository(ies) from history in {ElapsedMilliseconds:0.000} ms.",
                repositories.Count, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Success(
                new RepositoryHistorySnapshot(applicationState.LastRepositoryId, repositories.AsReadOnly()));
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to list recent repository history after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Unavailable<RepositoryHistorySnapshot>();
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to list recent repository history: stored metadata is malformed after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Invalid<RepositoryHistorySnapshot>();
        }
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var repository = await context.Repositories.SingleOrDefaultAsync(entry => entry.RepositoryId == repositoryId, cancellationToken);
            if (repository is not null)
            {
                context.Repositories.Remove(repository);
                await context.SaveChangesAsync(cancellationToken);
            }

            logger.LogDebug("Executed repository history removal for {RepositoryId}; removed {Removed}.", repositoryId, repository is not null);
            logger.LogInformation(
                "Removed repository {RepositoryId} from history (removed: {Removed}) in {ElapsedMilliseconds:0.000} ms.",
                repositoryId, repository is not null, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to remove repository {RepositoryId} from history after {ElapsedMilliseconds:0.000} ms.",
                repositoryId, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Unavailable();
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to remove repository {RepositoryId} from history: stored metadata is malformed after {ElapsedMilliseconds:0.000} ms.",
                repositoryId, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Invalid();
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetPreferredTargetAsync(string canonicalPathKey, string preferredTargetFullName, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var repository = await context.Repositories.SingleOrDefaultAsync(entry => entry.CanonicalPathKey == canonicalPathKey, cancellationToken);
            if (repository is not null)
            {
                repository.PreferredTargetFullName = preferredTargetFullName;
                await context.SaveChangesAsync(cancellationToken);
            }

            logger.LogDebug(
                "Updated preferred comparison target for repository {RepositoryId} to {TargetFullName} (matched: {Matched}).",
                repository?.RepositoryId, preferredTargetFullName, repository is not null);
            logger.LogInformation(
                "Updated preferred comparison target in history (matched: {Matched}) in {ElapsedMilliseconds:0.000} ms.",
                repository is not null, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to save a preferred comparison target after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Unavailable();
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to save a preferred comparison target: stored metadata is malformed after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Invalid();
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
