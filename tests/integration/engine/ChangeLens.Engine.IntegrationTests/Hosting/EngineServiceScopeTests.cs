using System.Diagnostics;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Engine.Hosting.Extensions;
using ChangeLens.Engine.IntegrationTests.Support;
using ChangeLens.Engine.Logging.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Repositories.Handlers;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Hosting;

/// <summary>
///     Verifies production engine composition for request scopes and SQLite behavior.
/// </summary>
public sealed class EngineServiceScopeTests
{
    /// <summary>
    ///     Asynchronously verifies scoped services are reused within one request and differ across requests.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RequestServicesFollowScopedLifetime()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var host = CreateHost(temporaryDirectory.DirectoryPath);
        await InitializeLocalStateAsync(host);

        await using var firstScope = host.Services.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<ChangeLensLocalStateDbContext>();
        var repeatedContext = firstScope.ServiceProvider.GetRequiredService<ChangeLensLocalStateDbContext>();
        var firstStore = firstScope.ServiceProvider.GetRequiredService<IRepositoryHistoryStore>();
        var repeatedStore = firstScope.ServiceProvider.GetRequiredService<IRepositoryHistoryStore>();
        var firstHandler = firstScope.ServiceProvider.GetRequiredKeyedService(
            typeof(IActionHandler),
            RepositoryOpenHandler.Action);
        var repeatedHandler = firstScope.ServiceProvider.GetRequiredKeyedService(
            typeof(IActionHandler),
            RepositoryOpenHandler.Action);

        await using var secondScope = host.Services.CreateAsyncScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ChangeLensLocalStateDbContext>();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<IRepositoryHistoryStore>();
        var secondHandler = secondScope.ServiceProvider.GetRequiredKeyedService(
            typeof(IActionHandler),
            RepositoryOpenHandler.Action);

        Assert.Same(firstContext, repeatedContext);
        Assert.Same(firstStore, repeatedStore);
        Assert.Same(firstHandler, repeatedHandler);
        Assert.NotSame(firstContext, secondContext);
        Assert.NotSame(firstStore, secondStore);
        Assert.NotSame(firstHandler, secondHandler);
    }

    /// <summary>
    ///     Asynchronously verifies foreign-key enforcement survives separate commands on one scoped context.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ForeignKeysRemainEnabledAcrossContextCommands()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var host = CreateHost(temporaryDirectory.DirectoryPath);
        await InitializeLocalStateAsync(host);
        await using var requestScope = host.Services.CreateAsyncScope();
        var context = requestScope.ServiceProvider.GetRequiredService<ChangeLensLocalStateDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        await context.Database.ExecuteSqlRawAsync("SELECT 1;", cancellationToken);

        var exception = await Assert.ThrowsAsync<SqliteException>(
            () => context.Database.ExecuteSqlRawAsync(
                "UPDATE application_state SET last_repository_id = 'missing' WHERE singleton_id = 1;",
                cancellationToken));

        Assert.Equal(19, exception.SqliteErrorCode);
    }

    /// <summary>
    ///     Asynchronously verifies competing writes honor the configured five-second command timeout.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompetingWriteWaitsForConfiguredCommandTimeout()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var host = CreateHost(temporaryDirectory.DirectoryPath);
        await InitializeLocalStateAsync(host);
        var paths = host.Services.GetRequiredService<LocalStatePaths>();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var lockingConnection = new SqliteConnection(
            $"Data Source={paths.DatabasePath};Mode=ReadWrite;Cache=Private");
        await lockingConnection.OpenAsync(cancellationToken);
        await using var lockingTransaction = await lockingConnection.BeginTransactionAsync(cancellationToken);
        await using var lockingCommand = lockingConnection.CreateCommand();
        lockingCommand.Transaction = (SqliteTransaction)lockingTransaction;
        lockingCommand.CommandText =
            "UPDATE application_state SET color_theme = 'light' WHERE singleton_id = 1;";
        await lockingCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var requestScope = host.Services.CreateAsyncScope();
        var context = requestScope.ServiceProvider.GetRequiredService<ChangeLensLocalStateDbContext>();
        var startedAt = Stopwatch.GetTimestamp();
        var exception = await Assert.ThrowsAsync<SqliteException>(
            () => context.Database.ExecuteSqlRawAsync(
                "UPDATE application_state SET color_theme = 'dark' WHERE singleton_id = 1;",
                cancellationToken));
        var elapsed = Stopwatch.GetElapsedTime(startedAt);

        Assert.Equal(5, exception.SqliteErrorCode);
        Assert.InRange(elapsed.TotalSeconds, 4.0, 9.0);
    }

    /// <summary>
    ///     Asynchronously verifies readiness reports a deleted database as unavailable without recreating it.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task StatusReportsDeletedDatabaseUnavailableWithoutRecreation()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var host = CreateHost(temporaryDirectory.DirectoryPath);
        await InitializeLocalStateAsync(host);
        var paths = host.Services.GetRequiredService<LocalStatePaths>();
        Assert.True(File.Exists(paths.DatabasePath));
        File.Delete(paths.DatabasePath);

        await using var requestScope = host.Services.CreateAsyncScope();
        var statusService = requestScope.ServiceProvider.GetRequiredService<IEngineStatusService>();
        var result = await statusService.CheckStatusAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LocalStateErrorCode.Unavailable, Assert.Single(result.Errors).Code);
        Assert.False(File.Exists(paths.DatabasePath));
    }

    private static IHost CreateHost(string localStateDirectory)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory,
            });
        builder.Configuration[LocalStateConstants.DirectoryConfigurationKey] = localStateDirectory;
        builder.Configuration[EngineLoggingConstants.FileDirectoryConfigurationKey] =
            Path.Combine(localStateDirectory, "logs");
        builder.AddEngine();
        return builder.Build();
    }

    private static async Task InitializeLocalStateAsync(IHost host)
    {
        await using var bootScope = host.Services.CreateAsyncScope();
        var initializer = bootScope.ServiceProvider.GetRequiredService<ILocalStateInitializer>();
        var result = await initializer.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
    }
}
