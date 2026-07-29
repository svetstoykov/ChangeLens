using ChangeLens.Core.LocalState.Models;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.LocalState.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.LocalState;

/// <summary>
///     Verifies the SQLite local-state lifecycle and approved metadata stores.
/// </summary>
public sealed class SqliteLocalStateTests
{
    /// <summary>
    ///     Verifies initial schema creation, idempotent readiness, history identity, removal, and theme round trips.
    /// </summary>
    [Fact]
    public async Task LocalStateRoundTripsApprovedMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = LocalStatePaths.Resolve(temporaryDirectory.DirectoryPath);
        await using var context = CreateContext(paths);
        var initializer = CreateInitializer(context, paths);
        var historyStore = new SqliteRepositoryHistoryStore(context, NullLogger<SqliteRepositoryHistoryStore>.Instance);
        var themeStore = new SqliteColorThemePreferenceStore(context, NullLogger<SqliteColorThemePreferenceStore>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True((await initializer.InitializeAsync(cancellationToken)).IsSuccess);
        Assert.True((await initializer.InitializeAsync(cancellationToken)).IsSuccess);

        var first = await historyStore.RecordOpenAsync(
            "/projects/change_lens",
            "/projects/change_lens",
            "change_lens",
            100,
            cancellationToken);
        Assert.True(first.IsSuccess);
        var second = await historyStore.RecordOpenAsync(
            "/projects/change_lens",
            "/projects/change_lens",
            "change_lens",
            200,
            cancellationToken);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Data!.RepositoryId, second.Data!.RepositoryId);

        Assert.True(
            (await historyStore.SetPreferredTargetAsync(
                "/projects/change_lens",
                "refs/heads/main",
                cancellationToken)).IsSuccess);
        var snapshot = await historyStore.ListRecentAsync(cancellationToken);
        Assert.True(snapshot.IsSuccess);
        var entry = Assert.Single(snapshot.Data!.Repositories);
        Assert.Equal(first.Data.RepositoryId, snapshot.Data.LastRepositoryId);
        Assert.Equal(200, entry.LastOpenedAtUnixMilliseconds);
        Assert.Equal("refs/heads/main", entry.PreferredTargetFullName);

        Assert.True((await themeStore.SetAsync(ColorTheme.Dark, cancellationToken)).IsSuccess);
        var theme = await themeStore.GetAsync(cancellationToken);
        Assert.True(theme.IsSuccess);
        Assert.Equal(ColorTheme.Dark, theme.Data);

        Assert.True(
            (await historyStore.RemoveAsync(first.Data.RepositoryId, cancellationToken)).IsSuccess);
        snapshot = await historyStore.ListRecentAsync(cancellationToken);
        Assert.Empty(snapshot.Data!.Repositories);
        Assert.Null(snapshot.Data.LastRepositoryId);
    }

    /// <summary>
    ///     Verifies the history cap and schema metadata stored in a fresh database.
    /// </summary>
    [Fact]
    public async Task LocalStateCreatesVersionOneAndPrunesToTwentyRepositories()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = LocalStatePaths.Resolve(temporaryDirectory.DirectoryPath);
        await using var context = CreateContext(paths);
        var initializer = CreateInitializer(context, paths);
        var historyStore = new SqliteRepositoryHistoryStore(context, NullLogger<SqliteRepositoryHistoryStore>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.True((await initializer.InitializeAsync(cancellationToken)).IsSuccess);

        for (var index = 0; index < 22; index++)
        {
            var result = await historyStore.RecordOpenAsync(
                $"/projects/repository-{index}",
                $"/projects/repository-{index}",
                $"repository-{index}",
                index,
                cancellationToken);
            Assert.True(result.IsSuccess);
        }

        var snapshot = await historyStore.ListRecentAsync(cancellationToken);
        Assert.Equal(20, snapshot.Data!.Repositories.Count);
        Assert.Equal("repository-21", snapshot.Data.Repositories[0].DisplayName);
        Assert.DoesNotContain(
            snapshot.Data.Repositories,
            entry => entry.DisplayName is "repository-0" or "repository-1");

        await using var connection = new SqliteConnection($"Data Source={paths.DatabasePath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT product_name, schema_version FROM local_state_metadata WHERE singleton_id = 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal("ChangeLens", reader.GetString(0));
        Assert.Equal(1, reader.GetInt32(1));
    }

    /// <summary>
    ///     Verifies that an existing schema without EF migration history is reported unavailable rather than adopted.
    /// </summary>
    [Fact]
    public async Task ExistingPreMigrationDatabaseReturnsUnavailable()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var databasePath = Path.Combine(temporaryDirectory.DirectoryPath, "changelens.db");
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
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
                VALUES (1, 'ChangeLens', 1, 100);
                INSERT INTO application_state (singleton_id, last_repository_id, color_theme)
                VALUES (1, NULL, 'dark');
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var paths = LocalStatePaths.Resolve(temporaryDirectory.DirectoryPath);
        await using var context = CreateContext(paths);
        var result = await CreateInitializer(context, paths).InitializeAsync(cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("localState.unavailable", Assert.Single(result.Errors).Code);
    }

    /// <summary>
    ///     Verifies malformed typed repository metadata returns the stable invalid-state error.
    /// </summary>
    [Fact]
    public async Task MalformedRepositoryMetadataReturnsInvalidLocalState()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = LocalStatePaths.Resolve(temporaryDirectory.DirectoryPath);
        await using var context = CreateContext(paths);
        var initializer = CreateInitializer(context, paths);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.True((await initializer.InitializeAsync(cancellationToken)).IsSuccess);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO repositories (
                repository_id,
                canonical_path,
                canonical_path_key,
                display_name,
                last_opened_at_unix_ms,
                preferred_target_full_name)
            VALUES ('not-a-guid', '/projects/invalid', '/projects/invalid', 'invalid', 1, NULL);
            """,
            cancellationToken);

        var result = await new SqliteRepositoryHistoryStore(context, NullLogger<SqliteRepositoryHistoryStore>.Instance)
            .ListRecentAsync(cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("localState.invalid", Assert.Single(result.Errors).Code);
    }

    private static ChangeLensLocalStateDbContext CreateContext(LocalStatePaths paths)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = 5,
            ForeignKeys = true,
        }.ToString();
        var options = new DbContextOptionsBuilder<ChangeLensLocalStateDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new ChangeLensLocalStateDbContext(options);
    }

    private static SqliteLocalStateInitializer CreateInitializer(
        ChangeLensLocalStateDbContext context,
        LocalStatePaths paths) =>
        new(context, paths, NullLogger<SqliteLocalStateInitializer>.Instance);
}
