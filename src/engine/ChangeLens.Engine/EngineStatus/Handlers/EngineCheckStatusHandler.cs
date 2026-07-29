using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Engine.EngineStatus.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.EngineStatus.Handlers;

/// <summary>
///     Handles the payload-free action that reports engine readiness.
/// </summary>
/// <remarks>
///     The action carries no payload, so supplied parameters are ignored. Local state is initialized once at host
///     startup, so this reports the live readiness service's own reachability check rather than repeating
///     initialization on the request path.
/// </remarks>
/// <param name="engineStatusService">The engine readiness capability. Cannot be <see langword="null" />.</param>
internal sealed class EngineCheckStatusHandler(IEngineStatusService engineStatusService) : IActionHandler
{
    /// <inheritdoc />
    public string Action => EngineStatusActionConstants.CheckStatusAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken) =>
        ProtocolResponseFactory.FromResult(request.RequestId, await engineStatusService.CheckStatusAsync(cancellationToken));
}
