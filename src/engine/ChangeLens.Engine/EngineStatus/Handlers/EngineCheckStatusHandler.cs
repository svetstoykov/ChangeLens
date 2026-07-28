using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.EngineStatus.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.EngineStatus.Handlers;

/// <summary>
///     Handles the payload-free action that reports engine readiness.
/// </summary>
/// <remarks>
///     <para>
///         The processor runs the readiness gate before it invokes any handler, so reaching this handler already
///         proves the engine is ready. The handler therefore only reports success.
///     </para>
///     <para>
///         The action carries no payload, so supplied parameters are ignored exactly as before.
///     </para>
/// </remarks>
internal sealed class EngineCheckStatusHandler : IActionHandler
{
    /// <inheritdoc />
    public string Action => EngineStatusActionConstants.CheckStatusAction;

    /// <inheritdoc />
    public Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(ProtocolResponseFactory.FromResult(request.RequestId, Result.Success()));
}
