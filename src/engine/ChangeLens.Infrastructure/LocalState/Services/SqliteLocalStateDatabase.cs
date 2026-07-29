using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Interfaces;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private static readonly OperationError UnavailableError = OperationError.ExternalDependencyFailure(
        "ChangeLens local state is unavailable. Review the Engine logs and retry.",
        LocalStateErrorCode.Unavailable);

    private static readonly OperationError InvalidError = OperationError.UnprocessableInput(
        "The existing local-state database contains invalid metadata.",
        LocalStateErrorCode.Invalid);

    private readonly string _directoryPath;
    private readonly string _databasePath;
    private readonly DbContextOptions<ChangeLensLocalStateDbContext> _contextOptions;
    private readonly ILogger<SqliteLocalStateDatabase> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _isReady;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SqliteLocalStateDatabase" /> class.
    /// </summary>
    /// <param name="options">The local-state settings. Cannot be <see langword="null" />.</param>
    /// <param name="logger">The local-state lifecycle logger.</param>
    public SqliteLocalStateDatabase(IOptions<LocalStateOptions> options, ILogger<SqliteLocalStateDatabase> logger)
    {
        this._logger = logger;
        var configuredDirectory = options.Value.Directory;
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this._directoryPath = Path.Combine(localApplicationDataPath, LocalStateConstants.ProductName);
        }
        else
        {
            this._directoryPath = Path.GetFullPath(configuredDirectory);
        }

        this._databasePath = Path.Combine(this._directoryPath, LocalStateConstants.DatabaseFileName);
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = this._databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = LocalStateConstants.CommandTimeoutSeconds,
        };
        this._contextOptions = new DbContextOptionsBuilder<ChangeLensLocalStateDbContext>()
            .UseSqlite(connectionStringBuilder.ToString())
            .Options;
    }

    /// <inheritdoc />
    public async Task<Result> InitializeAsync(CancellationToken cancellationToken)
    {
        await this._initializationLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(this._directoryPath);
            await using var context = await this.CreateContextAsync(cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);

            this._isReady = true;
            this._logger.LogInformation(
                "Local state is ready at schema version {SchemaVersion}.",
                LocalStateConstants.CurrentSchemaVersion);
            return Result.Success();
        }
        catch (Exception exception) when (IsExpectedAccessFailure(exception))
        {
            this._logger.LogInformation(
                "Local-state readiness failed with error {ErrorCode}.",
                LocalStateErrorCode.Unavailable);
            return Unavailable(exception);
        }
        finally
        {
            this._initializationLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<Result> CheckReadinessAsync(CancellationToken cancellationToken) =>
        this._isReady
            ? this.VerifyReadAsync(cancellationToken)
            : Task.FromResult(Unavailable());

    /// <inheritdoc />
    public async Task<ChangeLensLocalStateDbContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var context = new ChangeLensLocalStateDbContext(this._contextOptions);
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
    internal static Result Unavailable(Exception? exception = null) => Result.Fail(UnavailableError);

    /// <summary>
    ///     Maps an expected SQLite or filesystem access failure to a typed local-state result.
    /// </summary>
    /// <typeparam name="T">The discarded success payload type.</typeparam>
    /// <param name="exception">The expected access exception.</param>
    /// <returns>The stable unavailable result.</returns>
    internal static Result<T> Unavailable<T>(Exception? exception = null) => UnavailableError;

    /// <summary>
    ///     Maps malformed stored metadata to the stable invalid local-state result.
    /// </summary>
    /// <typeparam name="T">The discarded success payload type.</typeparam>
    /// <returns>The stable invalid local-state result.</returns>
    internal static Result<T> Invalid<T>() => InvalidError;

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
            await using var context = await this.CreateContextAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (IsExpectedAccessFailure(exception))
        {
            return Unavailable(exception);
        }
    }
}
