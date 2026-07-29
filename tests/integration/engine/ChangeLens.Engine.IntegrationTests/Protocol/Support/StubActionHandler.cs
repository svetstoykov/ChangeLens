using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Provides an action handler whose action name is chosen by the test.
/// </summary>
/// <remarks>
///     No approved handler can declare a blank or duplicated action name, so a controlled handler is the only way to
///     exercise the routing map's construction-time guards.
/// </remarks>
/// <param name="action">The action name this handler reports.</param>
internal sealed class StubActionHandler(
    string action,
    Func<EngineProtocolRequest, CancellationToken, Task<ProtocolResponse>>? handleAsync = null) : IActionHandler
{
    /// <inheritdoc />
    public string Action => action;

    /// <inheritdoc />
    public Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken) =>
        handleAsync?.Invoke(request, cancellationToken)
        ?? throw new NotSupportedException("The stub action handler is only used to build the routing map.");
}
