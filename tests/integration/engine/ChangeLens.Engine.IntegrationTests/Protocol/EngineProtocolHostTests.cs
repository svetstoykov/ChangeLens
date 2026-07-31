using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies scoped protocol action dispatch and exception handling.
/// </summary>
public sealed class EngineProtocolHostTests
{
    /// <summary>
    ///     Asynchronously verifies closed protocol input stops the application after the read loop completes.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ClosedStandardInputStopsTheApplication()
    {
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var application = Host.CreateApplicationBuilder().Build();
        var applicationLifetime = application.Services.GetRequiredService<IHostApplicationLifetime>();
        using var protocolHost = new EngineProtocolHost(
            new EngineProtocolTransport(
                input,
                output,
                new EngineProtocolSerializer(),
                NullLogger<EngineProtocolTransport>.Instance),
            application.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EngineProtocolHost>.Instance,
            applicationLifetime);

        await protocolHost.StartAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(protocolHost.ExecuteTask);
        await protocolHost.ExecuteTask.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(applicationLifetime.ApplicationStopping.IsCancellationRequested);
    }

    /// <summary>
    ///     Asynchronously verifies a known action is dispatched directly to its handler.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task KnownActionDispatchesDirectly()
    {
        var actionHandled = false;
        var handler = new RepositoryOpenStubActionHandler(
            (_, _) =>
            {
                actionHandled = true;
                return Task.FromResult<ProtocolResponse>(
                    new ProtocolResultResponse<string>(
                        EngineProtocolConstants.CurrentVersion,
                        EngineProtocolConstants.ResultResponseType,
                        "direct-dispatch",
                        "handled"));
            });
        var services = new ServiceCollection();
        services.AddKeyedScoped(typeof(IActionHandler), RepositoryOpenStubActionHandler.Action, (_, _) => handler);
        using var serviceProvider = services.BuildServiceProvider();
        using var requestScope = serviceProvider.CreateScope();
        var host = new EngineProtocolHost(
            null!,
            null!,
            NullLogger<EngineProtocolHost>.Instance,
            null!);

        var response = await host.ProcessAsync(
            CreateRequest(RepositoryOpenStubActionHandler.Action),
            requestScope.ServiceProvider,
            TestContext.Current.CancellationToken);

        Assert.True(actionHandled);
        Assert.IsType<ProtocolResultResponse<string>>(response);
    }

    /// <summary>
    ///     Asynchronously verifies a comparison-action exception is passed unchanged to the error logger.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ComparisonActionLogsOriginalException()
    {
        var expectedException = new InvalidOperationException("Comparison handler failed.");
        var handler = new ComparisonCheckFreshnessStubActionHandler(
            (_, _) => Task.FromException<ProtocolResponse>(expectedException));
        var services = new ServiceCollection();
        services.AddKeyedScoped(
            typeof(IActionHandler),
            ComparisonCheckFreshnessStubActionHandler.Action,
            (_, _) => handler);
        using var serviceProvider = services.BuildServiceProvider();
        using var requestScope = serviceProvider.CreateScope();
        var logger = new RecordingLogger<EngineProtocolHost>();
        var host = new EngineProtocolHost(null!, null!, logger, null!);

        await host.ProcessAsync(
            CreateRequest(ComparisonActionConstants.CheckFreshnessAction),
            requestScope.ServiceProvider,
            TestContext.Current.CancellationToken);

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Levels));
        Assert.Same(expectedException, Assert.Single(logger.Exceptions));
    }

    /// <summary>
    ///     Asynchronously verifies an unapproved keyed registration cannot expand the runtime protocol.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task UnapprovedKeyedHandlerCannotBeDispatched()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped(
            typeof(IActionHandler),
            UnapprovedStubActionHandler.Action,
            typeof(UnapprovedStubActionHandler));
        using var serviceProvider = services.BuildServiceProvider();
        using var requestScope = serviceProvider.CreateScope();
        var host = new EngineProtocolHost(
            null!,
            null!,
            NullLogger<EngineProtocolHost>.Instance,
            null!);

        var response = await host.ProcessAsync(
            CreateRequest(UnapprovedStubActionHandler.Action),
            requestScope.ServiceProvider,
            TestContext.Current.CancellationToken);

        var errorResponse = Assert.IsType<ProtocolErrorResponse>(response);
        Assert.Equal(EngineErrorCode.UnknownAction, Assert.Single(errorResponse.Errors).Code);
    }

    private static EngineProtocolRequest CreateRequest(string action) =>
        new()
        {
            ProtocolVersion = EngineProtocolConstants.CurrentVersion,
            RequestId = "direct-dispatch",
            Action = action,
        };
}
