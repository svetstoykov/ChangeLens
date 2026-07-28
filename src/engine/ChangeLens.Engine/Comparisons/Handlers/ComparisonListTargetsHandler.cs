using System.Text.Json;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Interfaces;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.Comparisons.Handlers;

/// <summary>
///     Handles the action that lists comparison targets within the protocol response ceiling.
/// </summary>
/// <remarks>
///     The host registers this handler as a singleton. The page builder measures the complete correlated response, so
///     the request identifier is passed through to it unchanged.
/// </remarks>
/// <param name="comparisonTargetDiscovery">
///     The comparison-target discovery capability. Cannot be <see langword="null" />.
/// </param>
/// <param name="comparisonTargetPageBuilder">The bounded target-page builder. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class ComparisonListTargetsHandler(
    IGitComparisonTargetDiscovery comparisonTargetDiscovery,
    IComparisonTargetPageBuilder comparisonTargetPageBuilder,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public string Action => ComparisonActionConstants.ListTargetsAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, this.Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ComparisonListTargetsParameters>(request.Parameters, this.Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var parameters = parametersResult.Data!;
        var discoveryResult = await comparisonTargetDiscovery.ListAsync(
            parameters.Path,
            parameters.Query,
            parameters.After,
            parameters.TargetSetToken,
            cancellationToken);
        if (discoveryResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<ComparisonTargetPageResult>(discoveryResult));
        }

        return ProtocolResponseFactory.FromResult(request.RequestId, comparisonTargetPageBuilder.Build(request.RequestId, discoveryResult.Data!));
    }
}
