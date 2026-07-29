using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies that the protocol host rejects an invalid handler registration when it is constructed.
/// </summary>
public sealed class EngineProtocolHostTests
{
    /// <summary>
    ///     Verifies that a handler without an action name is rejected and named in the failure.
    /// </summary>
    [Fact]
    public void ConstructionRejectsBlankHandlerAction()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateHost(new StubActionHandler(" ")));

        Assert.Contains(typeof(StubActionHandler).FullName!, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that two handlers claiming the same action are rejected and the action is named in the failure.
    /// </summary>
    [Fact]
    public void ConstructionRejectsDuplicateHandlerAction()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CreateHost(new StubActionHandler("repositories.open"), new StubActionHandler("repositories.open")));

        Assert.Contains("repositories.open", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Asynchronously verifies a known action is dispatched directly to its handler.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task KnownActionDispatchesDirectly()
    {
        var actionHandled = false;
        var handler = new StubActionHandler(
            "repositories.open",
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
        var host = new EngineProtocolHost(
            null!,
            [handler],
            NullLogger<EngineProtocolHost>.Instance,
            null!);

        var response = await host.ProcessAsync(CreateRequest("repositories.open"), TestContext.Current.CancellationToken);

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
        var handler = new StubActionHandler(
            ComparisonActionConstants.CheckFreshnessAction,
            (_, _) => Task.FromException<ProtocolResponse>(expectedException));
        var logger = new RecordingLogger<EngineProtocolHost>();
        var host = new EngineProtocolHost(null!, [handler], logger, null!);

        await host.ProcessAsync(
            CreateRequest(ComparisonActionConstants.CheckFreshnessAction),
            TestContext.Current.CancellationToken);

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Levels));
        Assert.Same(expectedException, Assert.Single(logger.Exceptions));
    }

    private static EngineProtocolHost CreateHost(params IActionHandler[] actionHandlers) =>
        new(
            null!,
            actionHandlers,
            NullLogger<EngineProtocolHost>.Instance,
            null!);

    private static EngineProtocolRequest CreateRequest(string action) =>
        new()
        {
            ProtocolVersion = EngineProtocolConstants.CurrentVersion,
            RequestId = "direct-dispatch",
            Action = action,
        };
}
