using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Infrastructure.EngineStatus.Services;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.LocalState.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.EngineStatus;

/// <summary>
///     Verifies <see cref="EngineStatusService" /> readiness checks and diagnostics against SQLite local state.
/// </summary>
public sealed class EngineStatusServiceTests
{
    /// <summary>
    ///     Verifies readiness logs elapsed time at Information on both healthy and unavailable database outcomes.
    /// </summary>
    [Fact]
    public async Task CheckStatusAsyncLogsInformationOutcomeAndElapsedTime()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = LocalStatePaths.Resolve(temporaryDirectory.DirectoryPath);
        await using var context = CreateContext(paths);
        var initializer = new SqliteLocalStateInitializer(context, paths, NullLogger<SqliteLocalStateInitializer>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.True((await initializer.InitializeAsync(cancellationToken)).IsSuccess);

        var logger = new RecordingInfrastructureLogger<EngineStatusService>();
        var service = new EngineStatusService(context, logger);

        var healthy = await service.CheckStatusAsync(cancellationToken);

        Assert.True(healthy.IsSuccess);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information && entry.Message.Contains(" ms.", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Debug);

        SqliteConnection.ClearAllPools();
        File.Delete(paths.DatabasePath);

        var unavailable = await service.CheckStatusAsync(cancellationToken);

        Assert.True(unavailable.IsFailure);
        Assert.Equal(LocalStateErrorCode.Unavailable, Assert.Single(unavailable.Errors).Code);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information
                && entry.Message.Contains(LocalStateErrorCode.Unavailable, StringComparison.Ordinal)
                && entry.Message.Contains(" ms.", StringComparison.Ordinal));
    }

    private static ChangeLensLocalStateDbContext CreateContext(LocalStatePaths paths)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = LocalStateConstants.CommandTimeoutSeconds,
            ForeignKeys = true,
        }.ToString();
        var options = new DbContextOptionsBuilder<ChangeLensLocalStateDbContext>().UseSqlite(connectionString).Options;
        return new ChangeLensLocalStateDbContext(options);
    }
}
