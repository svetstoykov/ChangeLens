using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Constants;
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
                logger.LogCritical("Local-state failed to enable write-ahead logging; reported mode was {JournalMode}.", journalMode);
                return LocalStateFailure.Unavailable();
            }

            logger.LogInformation("Local state is ready at schema version {SchemaVersion}.", LocalStateConstants.CurrentSchemaVersion);
            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            logger.LogInformation("Local-state readiness failed with error {ErrorCode}.", LocalStateErrorCode.Unavailable);
            return LocalStateFailure.Unavailable(exception);
        }
    }
}
