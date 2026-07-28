using System.Text.Json;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.Comparisons.Handlers;

/// <summary>
///     Handles the action that checks whether a prepared comparison is still current.
/// </summary>
/// <remarks>
///     The host registers this handler as a singleton. A freshness state the protocol has not approved is an internal
///     defect and reaches the processor's exception boundary.
/// </remarks>
/// <param name="comparisonFreshnessChecker">The comparison freshness capability. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class ComparisonCheckFreshnessHandler(
    IGitComparisonFreshnessChecker comparisonFreshnessChecker,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public string Action => ComparisonActionConstants.CheckFreshnessAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, this.Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ComparisonCheckFreshnessParameters>(request.Parameters, this.Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var parameters = parametersResult.Data!;
        var freshnessResult = await comparisonFreshnessChecker.CheckAsync(
            parameters.Path,
            parameters.Target,
            parameters.FreshnessToken,
            cancellationToken);
        if (freshnessResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<ComparisonFreshnessResult>(freshnessResult));
        }

        ComparisonFreshnessResult result = freshnessResult.Data switch
        {
            ComparisonFreshnessState.Current => new CurrentComparisonFreshnessResult(),
            ComparisonFreshnessState.Stale => new StaleComparisonFreshnessResult(),
            _ => throw new InvalidOperationException("The comparison freshness state is not approved for the engine protocol."),
        };
        return ProtocolResponseFactory.FromResult(request.RequestId, Result.Success(result));
    }
}
