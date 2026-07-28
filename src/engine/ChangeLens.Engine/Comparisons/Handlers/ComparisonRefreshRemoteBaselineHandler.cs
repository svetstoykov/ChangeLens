using System.Text.Json;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.Comparisons.Handlers;

/// <summary>
///     Handles the action that fetches a selected branch to refresh its cached remote-tracking baseline.
/// </summary>
/// <remarks>
///     The host registers this handler as a singleton. The refreshed revision is returned exactly as the tracker
///     reports it.
/// </remarks>
/// <param name="remoteBaselineTracker">
///     The remote baseline detection and refresh capability. Cannot be <see langword="null" />.
/// </param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class ComparisonRefreshRemoteBaselineHandler(
    IGitRemoteBaselineTracker remoteBaselineTracker,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public string Action => ComparisonActionConstants.RefreshRemoteBaselineAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, this.Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ComparisonRefreshRemoteBaselineParameters>(request.Parameters, this.Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var parameters = parametersResult.Data!;
        var refreshResult = await remoteBaselineTracker.RefreshAsync(parameters.Path, parameters.Target, cancellationToken);
        if (refreshResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<ComparisonRefreshRemoteBaselineResult>(refreshResult));
        }

        return ProtocolResponseFactory.FromResult(
            request.RequestId,
            Result.Success(new ComparisonRefreshRemoteBaselineResult(refreshResult.Data!)));
    }
}
