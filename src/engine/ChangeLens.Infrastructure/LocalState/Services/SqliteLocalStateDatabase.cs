using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Interfaces;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Provides the required Entity Framework SQLite local-state database lifecycle.
/// </summary>
/// <remarks>
///     The Engine registers this service as a singleton. Initialization is serialized and cached after success.
///     Store operations use independent bounded contexts.
/// </remarks>
public sealed class SqliteLocalStateDatabase : ILocalStateInitializer, ILocalStateDatabase
{
    private readonly string _directoryPath;
    private readonly string _databasePath;
    private readonly DbContextOptions<ChangeLensLocalStateDbContext> _contextOptions;
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
    public SqliteLocalStateDatabase(string? configuredDirectory, ILogger<SqliteLocalStateDatabase> logger)
    {
        _logger = logger;
        _directoryPath = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                LocalStateConstants.ProductName)
            : Path.GetFullPath(configuredDirectory);
        _databasePath = Path.Combine(_directoryPath, LocalStateConstants.DatabaseFileName);
        _contextOptions = new DbContextOptionsBuilder<ChangeLensLocalStateDbContext>()
            .UseSqlite(
                new SqliteConnectionStringBuilder
                {
                    DataSource = _databasePath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Private,
                    DefaultTimeout = LocalStateConstants.CommandTimeoutSeconds,
                }.ToString())
            .Options;
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
                await using var context = await CreateContextAsync(cancellationToken);
                var preparation = await PrepareExistingDatabaseAsync(context, databaseExisted, cancellationToken);
                if (preparation.IsFailure)
                {
                    return preparation;
                }

                await context.Database.MigrateAsync(cancellationToken);
                var verification = await VerifySchemaAsync(context, cancellationToken);
                if (verification.IsFailure)
                {
                    return verification;
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
                return Unavailable(exception);
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ChangeLensLocalStateDbContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var context = new ChangeLensLocalStateDbContext(_contextOptions);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;",
            cancellationToken);
        return context;
    }

    /// <summary>
    ///     Maps an expected SQLite or filesystem access failure to the stable local-state result.
    /// </summary>
    /// <param name="exception">The expected access exception.</param>
    /// <returns>The stable unavailable result.</returns>
    internal static Result Unavailable(Exception? exception = null) =>
        Result.Fail(OperationError.ExternalDependencyFailure(
            "ChangeLens local state is unavailable. Review the Engine logs and retry.",
            LocalStateErrorCode.Unavailable));

    /// <summary>
    ///     Maps an expected SQLite or filesystem access failure to a typed local-state result.
    /// </summary>
    /// <typeparam name="T">The discarded success payload type.</typeparam>
    /// <param name="exception">The expected access exception.</param>
    /// <returns>The stable unavailable result.</returns>
    internal static Result<T> Unavailable<T>(Exception? exception = null) =>
        Result.Fail<T>(OperationError.ExternalDependencyFailure(
            "ChangeLens local state is unavailable. Review the Engine logs and retry.",
            LocalStateErrorCode.Unavailable));

    /// <summary>
    ///     Maps malformed stored metadata to the stable invalid local-state result.
    /// </summary>
    /// <typeparam name="T">The discarded success payload type.</typeparam>
    /// <returns>The stable invalid local-state result.</returns>
    internal static Result<T> Invalid<T>() =>
        Result.Fail<T>(OperationError.UnprocessableInput(
            "The existing local-state database contains invalid metadata.",
            LocalStateErrorCode.Invalid));

    /// <summary>
    ///     Determines whether an exception is an expected local database access failure.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true" /> when the exception is an expected local-state failure.</returns>
    internal static bool IsExpectedAccessFailure(Exception exception) =>
        exception is SqliteException or IOException or UnauthorizedAccessException or DbUpdateException;

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
            await using var context = await CreateContextAsync(cancellationToken);
            return await VerifySchemaAsync(context, cancellationToken);
        }
        catch (Exception exception) when (IsExpectedAccessFailure(exception))
        {
            return Unavailable(exception);
        }
    }

    private static async Task<Result> PrepareExistingDatabaseAsync(
        ChangeLensLocalStateDbContext context,
        bool databaseExisted,
        CancellationToken cancellationToken)
    {
        if (!databaseExisted)
        {
            return Result.Success();
        }

        var tableNames = await GetTableNamesAsync(context, cancellationToken);
        if (tableNames.Count == 0)
        {
            return InvalidSchema();
        }

        var requiredTables = new[] { "local_state_metadata", "repositories", "application_state" };
        if (!requiredTables.All(tableNames.Contains))
        {
            return InvalidSchema();
        }

        var metadata = await context.Metadata.AsNoTracking().SingleOrDefaultAsync(
            entry => entry.SingletonId == 1,
            cancellationToken);
        if (metadata is null || metadata.ProductName != LocalStateConstants.ProductName || metadata.SchemaVersion < 1)
        {
            return InvalidSchema();
        }

        if (metadata.SchemaVersion > LocalStateConstants.CurrentSchemaVersion)
        {
            return Result.Fail(OperationError.InvalidOperation(
                "The local-state database was created by a newer ChangeLens version.",
                LocalStateErrorCode.VersionUnsupported));
        }

        if (!tableNames.Contains("__EFMigrationsHistory"))
        {
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE __EFMigrationsHistory (MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY, ProductVersion TEXT NOT NULL);",
                cancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1});",
                [LocalStateConstants.InitialMigrationId, "10.0.10"],
                cancellationToken);
        }

        return Result.Success();
    }

    private static async Task<HashSet<string>> GetTableNamesAsync(
        ChangeLensLocalStateDbContext context,
        CancellationToken cancellationToken)
    {
        var tableNames = new HashSet<string>(StringComparer.Ordinal);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static async Task<Result> VerifySchemaAsync(
        ChangeLensLocalStateDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await context.Metadata.AsNoTracking().SingleOrDefaultAsync(
                entry => entry.SingletonId == 1,
                cancellationToken);
            var applicationStateCount = await context.ApplicationState.CountAsync(
                entry => entry.SingletonId == 1,
                cancellationToken);
            return metadata is null ||
                   metadata.ProductName != LocalStateConstants.ProductName ||
                   metadata.SchemaVersion != LocalStateConstants.CurrentSchemaVersion ||
                   applicationStateCount != 1
                ? InvalidSchema()
                : Result.Success();
        }
        catch (SqliteException exception) when (!IsBusy(exception))
        {
            return InvalidSchema();
        }
    }

    private static Result InvalidSchema() =>
        Result.Fail(OperationError.UnprocessableInput(
            "The existing local-state database schema is invalid.",
            LocalStateErrorCode.Invalid));

    private static bool IsBusy(SqliteException exception) => exception.SqliteErrorCode is 5 or 6;
}
