using System.Diagnostics;
using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Helpers;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Creates and migrates the required SQLite local-state database at engine startup.
/// </summary>
/// <remarks>
///     The Engine resolves this scoped service from a dedicated boot scope. It is called once and does not need to be
///     thread-safe.
/// </remarks>
/// <param name="context">The scoped local-state context. Cannot be <see langword="null" />.</param>
/// <param name="paths">The immutable local-state paths. Cannot be <see langword="null" />.</param>
/// <param name="logger">The local-state lifecycle logger. Cannot be <see langword="null" />.</param>
public sealed class SqliteLocalStateInitializer(
    ChangeLensLocalStateDbContext context,
    LocalStatePaths paths,
    ILogger<SqliteLocalStateInitializer> logger) : ILocalStateInitializer
{
    /// <inheritdoc />
    public async Task<Result> InitializeAsync(CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        logger.LogDebug("Initializing SQLite local-state database and enabling WAL journal mode.");

        try
        {
            Directory.CreateDirectory(paths.DirectoryPath);
            await context.Database.MigrateAsync(cancellationToken);

            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
            var journalMode = (await context.Database
                .SqlQueryRaw<string>("PRAGMA journal_mode;")
                .ToListAsync(cancellationToken))
                .FirstOrDefault();

            if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogCritical(
                    "Local-state failed to enable write-ahead logging; reported mode was {JournalMode} after {ElapsedMilliseconds:0.000} ms.",
                    journalMode, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return LocalStateFailure.Unavailable();
            }

            logger.LogInformation(
                "Local state is ready at schema version {SchemaVersion} in {ElapsedMilliseconds:0.000} ms.",
                LocalStateConstants.CurrentSchemaVersion, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug(exception, "Local-state initialization failed with an expected access failure.");
            logger.LogInformation(
                "Local-state readiness failed with error {ErrorCode} in {ElapsedMilliseconds:0.000} ms.",
                LocalStateErrorCode.Unavailable, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Unavailable();
        }
    }
}
