using ChangeLens.Core.LocalState.Models;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using ChangeLens.Infrastructure.LocalState.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.LocalState;

/// <summary>
///     Verifies the SQLite local-state lifecycle and approved metadata stores.
/// </summary>
public sealed class SqliteLocalStateTests
{
    /// <summary>
    ///     Verifies that an existing empty database is rejected without destructive replacement.
    /// </summary>
    [Fact]
    public async Task ExistingEmptyDatabaseRemainsUntouchedAndInvalid()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var databasePath = Path.Combine(temporaryDirectory.DirectoryPath, "changelens.db");
        await File.WriteAllBytesAsync(
            databasePath,
            [],
            TestContext.Current.CancellationToken);
        var database = CreateDatabase(temporaryDirectory.DirectoryPath);

        var result = await database.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("localState.invalid", Assert.Single(result.Errors).Code);
        Assert.Equal(0, new FileInfo(databasePath).Length);
    }

    /// <summary>
    ///     Verifies initial schema creation, idempotent readiness, history identity, removal, and theme round trips.
    /// </summary>
    [Fact]
    public async Task LocalStateRoundTripsApprovedMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var database = CreateDatabase(temporaryDirectory.DirectoryPath);
        var historyStore = new SqliteRepositoryHistoryStore(database);
        var themeStore = new SqliteColorThemePreferenceStore(database);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True((await database.InitializeAsync(cancellationToken)).IsSuccess);
        Assert.True((await database.InitializeAsync(cancellationToken)).IsSuccess);

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
        var database = CreateDatabase(temporaryDirectory.DirectoryPath);
        var historyStore = new SqliteRepositoryHistoryStore(database);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.True((await database.InitializeAsync(cancellationToken)).IsSuccess);

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

        var databasePath = Path.Combine(temporaryDirectory.DirectoryPath, "changelens.db");
        await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
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
    ///     Verifies that a newer schema version is rejected and remains unchanged.
    /// </summary>
    [Fact]
    public async Task NewerDatabaseVersionRemainsUntouchedAndUnsupported()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var database = CreateDatabase(temporaryDirectory.DirectoryPath);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.True((await database.InitializeAsync(cancellationToken)).IsSuccess);
        var databasePath = Path.Combine(temporaryDirectory.DirectoryPath, "changelens.db");

        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE local_state_metadata SET schema_version = 2 WHERE singleton_id = 1;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var result = await CreateDatabase(temporaryDirectory.DirectoryPath)
            .InitializeAsync(cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("localState.versionUnsupported", Assert.Single(result.Errors).Code);
        await using var verificationConnection =
            new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        await verificationConnection.OpenAsync(cancellationToken);
        await using var verificationCommand = verificationConnection.CreateCommand();
        verificationCommand.CommandText =
            "SELECT schema_version FROM local_state_metadata WHERE singleton_id = 1;";
        Assert.Equal(2L, await verificationCommand.ExecuteScalarAsync(cancellationToken));
    }

    /// <summary>
    ///     Verifies malformed typed repository metadata returns the stable invalid-state error.
    /// </summary>
    [Fact]
    public async Task MalformedRepositoryMetadataReturnsInvalidLocalState()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var database = CreateDatabase(temporaryDirectory.DirectoryPath);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.True((await database.InitializeAsync(cancellationToken)).IsSuccess);

        await using (var connection = await database.OpenConnectionAsync(cancellationToken))
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO repositories (
                    repository_id,
                    canonical_path,
                    canonical_path_key,
                    display_name,
                    last_opened_at_unix_ms,
                    preferred_target_full_name)
                VALUES ('not-a-guid', '/projects/invalid', '/projects/invalid', 'invalid', 1, NULL);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var result = await new SqliteRepositoryHistoryStore(database)
            .ListRecentAsync(cancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("localState.invalid", Assert.Single(result.Errors).Code);
    }

    private static SqliteLocalStateDatabase CreateDatabase(string directoryPath) =>
        new(directoryPath, NullLogger<SqliteLocalStateDatabase>.Instance);
}
