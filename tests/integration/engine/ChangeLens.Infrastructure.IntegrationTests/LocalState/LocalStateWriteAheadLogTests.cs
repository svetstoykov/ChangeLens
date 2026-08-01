using ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.LocalState.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.LocalState;

/// <summary>
///     Verifies that local-state initialization enables and confirms SQLite write-ahead logging.
/// </summary>
public sealed class LocalStateWriteAheadLogTests
{
    /// <summary>
    ///     Verifies that initialization enables write-ahead logging and that the analysis run entities are mapped.
    /// </summary>
    [Fact]
    public async Task InitializationEnablesAndVerifiesWriteAheadLogging()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = LocalStatePaths.Resolve(temporaryDirectory.DirectoryPath);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = LocalStateConstants.CommandTimeoutSeconds,
            ForeignKeys = true,
        }.ToString();
        var options = new DbContextOptionsBuilder<ChangeLensLocalStateDbContext>().UseSqlite(connectionString).Options;
        await using var context = new ChangeLensLocalStateDbContext(options);
        var initializer = new SqliteLocalStateInitializer(
            context,
            paths,
            NullLogger<SqliteLocalStateInitializer>.Instance);

        var result = await initializer.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var journalMode = (await context.Database
            .SqlQueryRaw<string>("PRAGMA journal_mode;")
            .ToListAsync(TestContext.Current.CancellationToken))
            .Single();
        Assert.Equal("wal", journalMode, ignoreCase: true);
        Assert.True(context.Model.FindEntityType(typeof(AnalysisRunEntity)) is not null);
    }
}
