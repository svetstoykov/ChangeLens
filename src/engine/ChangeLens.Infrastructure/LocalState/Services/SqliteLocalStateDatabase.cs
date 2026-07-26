using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Provides the required SQLite local-state database lifecycle.
/// </summary>
/// <remarks>
///     The Engine registers this service as a singleton. Initialization is serialized and cached after success.
///     Store operations use independent bounded connections.
/// </remarks>
public sealed class SqliteLocalStateDatabase : ILocalStateInitializer, ILocalStateDatabase
{
    private readonly string _directoryPath;
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly ILogger<SqliteLocalStateDatabase> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _isReady;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SqliteLocalStateDatabase" /> class.
    /// </summary>
    /// <param name="configuredDirectory">
    ///     The configured database directory, or <see langword="null" /> to use local application data.
    /// </param>
    /// <param name="logger">The local-state lifecycle logger.</param>
    public SqliteLocalStateDatabase(
        string? configuredDirectory,
        ILogger<SqliteLocalStateDatabase> logger)
    {
        _logger = logger;
        _directoryPath = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                LocalStateConstants.ProductName)
            : Path.GetFullPath(configuredDirectory);
        _databasePath = Path.Combine(_directoryPath, LocalStateConstants.DatabaseFileName);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = LocalStateConstants.CommandTimeoutSeconds,
        }.ToString();
    }

    /// <inheritdoc />
    public async Task<Result> InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isReady)
        {
            return await VerifyReadAsync(cancellationToken);
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isReady)
            {
                return await VerifyReadAsync(cancellationToken);
            }

            try
            {
                Directory.CreateDirectory(_directoryPath);
                var databaseExisted = File.Exists(_databasePath);
                await using var connection = await OpenConnectionAsync(cancellationToken);
                var metadataState = await ReadMetadataStateAsync(connection, cancellationToken);
                if (metadataState.IsFailure)
                {
                    return metadataState;
                }

                if (metadataState.Data is null)
                {
                    if (databaseExisted)
                    {
                        return InvalidSchema();
                    }

                    var createResult = await CreateVersionOneAsync(connection, cancellationToken);
                    if (createResult.IsFailure)
                    {
                        return createResult;
                    }
                }
                else if (metadataState.Data.Value > LocalStateConstants.CurrentSchemaVersion)
                {
                    return Result.Fail(
                        OperationError.InvalidOperation(
                            "The local-state database was created by a newer ChangeLens version.",
                            LocalStateErrorCode.VersionUnsupported));
                }

                var verifyResult = await VerifySchemaAsync(connection, cancellationToken);
                if (verifyResult.IsFailure)
                {
                    return verifyResult;
                }

                _isReady = true;
                _logger.LogInformation(
                    "Local state is ready at schema version {SchemaVersion}.",
                    LocalStateConstants.CurrentSchemaVersion);
                return Result.Success();
            }
            catch (Exception exception) when (IsExpectedAccessFailure(exception))
            {
                _logger.LogInformation(
                    "Local-state readiness failed with error {ErrorCode}.",
                    LocalStateErrorCode.Unavailable);
                return Unavailable();
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        command.CommandTimeout = LocalStateConstants.CommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    ///     Maps an expected SQLite or filesystem access failure to the stable local-state result.
    /// </summary>
    /// <param name="exception">The expected access exception.</param>
    /// <returns>The stable unavailable result.</returns>
    internal static Result Unavailable(Exception? exception = null) =>
        Result.Fail(
            OperationError.ExternalDependencyFailure(
                "ChangeLens local state is unavailable. Review the Engine logs and retry.",
                LocalStateErrorCode.Unavailable));

    /// <summary>
    ///     Maps an expected SQLite or filesystem access failure to a typed local-state result.
    /// </summary>
    /// <typeparam name="T">The discarded success payload type.</typeparam>
    /// <param name="exception">The expected access exception.</param>
    /// <returns>The stable unavailable result.</returns>
    internal static Result<T> Unavailable<T>(Exception? exception = null) =>
        Result.Fail<T>(
            OperationError.ExternalDependencyFailure(
                "ChangeLens local state is unavailable. Review the Engine logs and retry.",
                LocalStateErrorCode.Unavailable));

    /// <summary>
    ///     Maps malformed stored metadata to the stable invalid local-state result.
    /// </summary>
    /// <typeparam name="T">The discarded success payload type.</typeparam>
    /// <returns>The stable invalid local-state result.</returns>
    internal static Result<T> Invalid<T>() =>
        Result.Fail<T>(
            OperationError.UnprocessableInput(
                "The existing local-state database contains invalid metadata.",
                LocalStateErrorCode.Invalid));

    /// <summary>
    ///     Determines whether an exception is an expected local database access failure.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true" /> when the exception is an expected local-state failure.</returns>
    internal static bool IsExpectedAccessFailure(Exception exception) =>
        exception is SqliteException or IOException or UnauthorizedAccessException;

    /// <summary>
    ///     Determines whether an exception represents malformed typed metadata read from SQLite.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true" /> when stored metadata cannot satisfy its owned model.</returns>
    internal static bool IsMalformedDataFailure(Exception exception) =>
        exception is FormatException or InvalidCastException or OverflowException;

    private async Task<Result> VerifyReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            return await VerifySchemaAsync(connection, cancellationToken);
        }
        catch (Exception exception) when (IsExpectedAccessFailure(exception))
        {
            return Unavailable(exception);
        }
    }

    private static async Task<Result<int?>> ReadMetadataStateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'local_state_metadata';";
        tableCommand.CommandTimeout = LocalStateConstants.CommandTimeoutSeconds;
        var exists = Convert.ToInt64(await tableCommand.ExecuteScalarAsync(cancellationToken)) == 1;
        if (!exists)
        {
            await using var anyTableCommand = connection.CreateCommand();
            anyTableCommand.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
            var anyTables =
                Convert.ToInt64(await anyTableCommand.ExecuteScalarAsync(cancellationToken)) > 0;
            return anyTables
                ? Result.Fail<int?>(
                    OperationError.UnprocessableInput(
                        "The existing local-state database is not a valid ChangeLens database.",
                        LocalStateErrorCode.Invalid))
                : Result.Success<int?>(null);
        }

        try
        {
            await using var metadataCommand = connection.CreateCommand();
            metadataCommand.CommandText =
                "SELECT product_name, schema_version FROM local_state_metadata WHERE singleton_id = 1;";
            await using var reader = await metadataCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) ||
                !string.Equals(reader.GetString(0), LocalStateConstants.ProductName, StringComparison.Ordinal))
            {
                return InvalidMetadata();
            }

            var version = reader.GetInt32(1);
            return version < 1 ? InvalidMetadata() : Result.Success<int?>(version);
        }
        catch (SqliteException exception) when (!IsBusy(exception))
        {
            return InvalidMetadata();
        }
    }

    private static async Task<Result> CreateVersionOneAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = LocalStateConstants.CommandTimeoutSeconds;
            command.CommandText =
                """
                CREATE TABLE local_state_metadata (
                    singleton_id INTEGER NOT NULL PRIMARY KEY CHECK (singleton_id = 1),
                    product_name TEXT NOT NULL CHECK (product_name = 'ChangeLens'),
                    schema_version INTEGER NOT NULL CHECK (schema_version > 0),
                    created_at_unix_ms INTEGER NOT NULL
                );
                CREATE TABLE repositories (
                    repository_id TEXT NOT NULL PRIMARY KEY,
                    canonical_path TEXT NOT NULL,
                    canonical_path_key TEXT NOT NULL UNIQUE,
                    display_name TEXT NOT NULL,
                    last_opened_at_unix_ms INTEGER NOT NULL,
                    preferred_target_full_name TEXT NULL
                );
                CREATE TABLE application_state (
                    singleton_id INTEGER NOT NULL PRIMARY KEY CHECK (singleton_id = 1),
                    last_repository_id TEXT NULL REFERENCES repositories(repository_id) ON DELETE SET NULL,
                    color_theme TEXT NULL CHECK (color_theme IS NULL OR color_theme IN ('light', 'dark'))
                );
                INSERT INTO local_state_metadata
                    (singleton_id, product_name, schema_version, created_at_unix_ms)
                VALUES (1, 'ChangeLens', 1, $createdAt);
                INSERT INTO application_state (singleton_id, last_repository_id, color_theme)
                VALUES (1, NULL, NULL);
                """;
            command.Parameters.AddWithValue(
                "$createdAt",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (exception is SqliteException or InvalidOperationException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result.Fail(
                OperationError.ExternalDependencyFailure(
                    "The ChangeLens local-state schema could not be created.",
                    LocalStateErrorCode.MigrationFailed));
        }
    }

    private static async Task<Result> VerifySchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    (SELECT schema_version FROM local_state_metadata WHERE singleton_id = 1),
                    (SELECT COUNT(*) FROM application_state WHERE singleton_id = 1),
                    (SELECT group_concat(name, ',') FROM (
                        SELECT name FROM pragma_table_info('local_state_metadata') ORDER BY cid)),
                    (SELECT group_concat(name, ',') FROM (
                        SELECT name FROM pragma_table_info('repositories') ORDER BY cid)),
                    (SELECT group_concat(name, ',') FROM (
                        SELECT name FROM pragma_table_info('application_state') ORDER BY cid)),
                    (SELECT COUNT(*) FROM pragma_foreign_key_list('application_state')
                        WHERE "table" = 'repositories'
                          AND "from" = 'last_repository_id'
                          AND "to" = 'repository_id'
                          AND on_delete = 'SET NULL');
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) ||
                reader.GetInt32(0) != LocalStateConstants.CurrentSchemaVersion ||
                reader.GetInt64(1) != 1 ||
                reader.GetString(2) !=
                    "singleton_id,product_name,schema_version,created_at_unix_ms" ||
                reader.GetString(3) !=
                    "repository_id,canonical_path,canonical_path_key,display_name," +
                    "last_opened_at_unix_ms,preferred_target_full_name" ||
                reader.GetString(4) != "singleton_id,last_repository_id,color_theme" ||
                reader.GetInt64(5) != 1)
            {
                return InvalidSchema();
            }

            return Result.Success();
        }
        catch (SqliteException exception) when (!IsBusy(exception))
        {
            return InvalidSchema();
        }
    }

    private static Result<int?> InvalidMetadata() =>
        Result.Fail<int?>(
            OperationError.UnprocessableInput(
                "The existing local-state database metadata is invalid.",
                LocalStateErrorCode.Invalid));

    private static Result InvalidSchema() =>
        Result.Fail(
            OperationError.UnprocessableInput(
                "The existing local-state database schema is invalid.",
                LocalStateErrorCode.Invalid));

    private static bool IsBusy(SqliteException exception) =>
        exception.SqliteErrorCode is 5 or 6;
}
