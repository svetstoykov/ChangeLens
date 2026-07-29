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
///     The host registers this handler as scoped. The action carries no payload, so supplied parameters are ignored.
///     Local state is initialized once at host startup, and this reports the request context's live reachability
///     check.
/// </remarks>
/// <param name="engineStatusService">The engine readiness capability. Cannot be <see langword="null" />.</param>
internal sealed class EngineCheckStatusHandler(IEngineStatusService engineStatusService) : IActionHandler
{
    /// <inheritdoc />
    public static string Action => EngineStatusActionConstants.CheckStatusAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken) =>
        ProtocolResponseFactory.FromResult(request.RequestId, await engineStatusService.CheckStatusAsync(cancellationToken));
}
