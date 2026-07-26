using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Interfaces;
using Microsoft.Data.Sqlite;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Stores repository history in the required SQLite local-state database.
/// </summary>
/// <remarks>
///     The Engine registers this stateless implementation as a singleton. Each operation uses its own connection.
/// </remarks>
/// <param name="database">The required SQLite local-state database.</param>
public sealed class SqliteRepositoryHistoryStore(ILocalStateDatabase database)
    : IRepositoryHistoryStore
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
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            var existing = await FindByPathKeyAsync(
                connection,
                transaction,
                canonicalPathKey,
                cancellationToken);
            var repositoryId = existing?.RepositoryId ?? Guid.NewGuid();
            var preferredTarget = existing?.PreferredTargetFullName;

            await using (var upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandTimeout = LocalStateConstants.CommandTimeoutSeconds;
                upsert.CommandText =
                    """
                    INSERT INTO repositories (
                        repository_id,
                        canonical_path,
                        canonical_path_key,
                        display_name,
                        last_opened_at_unix_ms,
                        preferred_target_full_name)
                    VALUES ($id, $path, $pathKey, $name, $openedAt, $preferredTarget)
                    ON CONFLICT(canonical_path_key) DO UPDATE SET
                        canonical_path = excluded.canonical_path,
                        display_name = excluded.display_name,
                        last_opened_at_unix_ms = excluded.last_opened_at_unix_ms;
                    """;
                upsert.Parameters.AddWithValue("$id", repositoryId.ToString("D"));
                upsert.Parameters.AddWithValue("$path", canonicalPath);
                upsert.Parameters.AddWithValue("$pathKey", canonicalPathKey);
                upsert.Parameters.AddWithValue("$name", displayName);
                upsert.Parameters.AddWithValue("$openedAt", openedAtUnixMilliseconds);
                upsert.Parameters.AddWithValue(
                    "$preferredTarget",
                    (object?)preferredTarget ?? DBNull.Value);
                await upsert.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var selectLast = connection.CreateCommand())
            {
                selectLast.Transaction = transaction;
                selectLast.CommandText =
                    "UPDATE application_state SET last_repository_id = $id WHERE singleton_id = 1;";
                selectLast.Parameters.AddWithValue("$id", repositoryId.ToString("D"));
                await selectLast.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var prune = connection.CreateCommand())
            {
                prune.Transaction = transaction;
                prune.CommandText =
                    """
                    DELETE FROM repositories
                    WHERE repository_id IN (
                        SELECT repository_id
                        FROM repositories
                        ORDER BY last_opened_at_unix_ms DESC, repository_id ASC
                        LIMIT -1 OFFSET $maximum
                    );
                    """;
                prune.Parameters.AddWithValue("$maximum", LocalStateConstants.MaximumRecentRepositories);
                await prune.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success(
                new RepositoryHistoryEntry(
                    repositoryId,
                    canonicalPath,
                    displayName,
                    openedAtUnixMilliseconds,
                    preferredTarget));
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            return SqliteLocalStateDatabase.Unavailable<RepositoryHistoryEntry>(exception);
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsMalformedDataFailure(exception))
        {
            return SqliteLocalStateDatabase.Invalid<RepositoryHistoryEntry>();
        }
    }

    /// <inheritdoc />
    public async Task<Result<RepositoryHistoryEntry?>> GetLastAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    repositories.repository_id,
                    repositories.canonical_path,
                    repositories.display_name,
                    repositories.last_opened_at_unix_ms,
                    repositories.preferred_target_full_name
                FROM application_state
                LEFT JOIN repositories
                    ON repositories.repository_id = application_state.last_repository_id
                WHERE application_state.singleton_id = 1;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
            {
                return Result.Success<RepositoryHistoryEntry?>(null);
            }

            return Result.Success<RepositoryHistoryEntry?>(ReadEntry(reader));
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            return SqliteLocalStateDatabase.Unavailable<RepositoryHistoryEntry?>(exception);
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsMalformedDataFailure(exception))
        {
            return SqliteLocalStateDatabase.Invalid<RepositoryHistoryEntry?>();
        }
    }

    /// <inheritdoc />
    public async Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            Guid? lastRepositoryId;
            await using (var lastCommand = connection.CreateCommand())
            {
                lastCommand.CommandText =
                    "SELECT last_repository_id FROM application_state WHERE singleton_id = 1;";
                var value = await lastCommand.ExecuteScalarAsync(cancellationToken);
                lastRepositoryId = value is string id ? Guid.ParseExact(id, "D") : null;
            }

            var entries = new List<RepositoryHistoryEntry>();
            await using var listCommand = connection.CreateCommand();
            listCommand.CommandText =
                """
                SELECT
                    repository_id,
                    canonical_path,
                    display_name,
                    last_opened_at_unix_ms,
                    preferred_target_full_name
                FROM repositories
                ORDER BY last_opened_at_unix_ms DESC, repository_id ASC
                LIMIT 20;
                """;
            await using var reader = await listCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(ReadEntry(reader));
            }

            return Result.Success(
                new RepositoryHistorySnapshot(lastRepositoryId, entries.AsReadOnly()));
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            return SqliteLocalStateDatabase.Unavailable<RepositoryHistorySnapshot>(exception);
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsMalformedDataFailure(exception))
        {
            return SqliteLocalStateDatabase.Invalid<RepositoryHistorySnapshot>();
        }
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM repositories WHERE repository_id = $id;";
            command.Parameters.AddWithValue("$id", repositoryId.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
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
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE repositories
                SET preferred_target_full_name = $target
                WHERE canonical_path_key = $pathKey;
                """;
            command.Parameters.AddWithValue("$target", preferredTargetFullName);
            command.Parameters.AddWithValue("$pathKey", canonicalPathKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            return SqliteLocalStateDatabase.Unavailable(exception);
        }
    }

    private static async Task<RepositoryHistoryEntry?> FindByPathKeyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string canonicalPathKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                repository_id,
                canonical_path,
                display_name,
                last_opened_at_unix_ms,
                preferred_target_full_name
            FROM repositories
            WHERE canonical_path_key = $pathKey;
            """;
        command.Parameters.AddWithValue("$pathKey", canonicalPathKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadEntry(reader) : null;
    }

    private static RepositoryHistoryEntry ReadEntry(SqliteDataReader reader) =>
        new(
            Guid.ParseExact(reader.GetString(0), "D"),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt64(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
}
