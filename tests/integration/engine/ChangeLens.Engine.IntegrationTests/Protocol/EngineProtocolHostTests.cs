using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Services;
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

    private static EngineProtocolHost CreateHost(params IActionHandler[] actionHandlers) =>
        new(
            null!,
            actionHandlers,
            new StubEngineStatusService(),
            NullLogger<EngineProtocolHost>.Instance,
            null!);
}
