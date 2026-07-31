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
///     The host registers this handler as scoped. A freshness state the protocol has not approved is returned as a
///     domain-coded internal error.
/// </remarks>
/// <param name="comparisonFreshnessChecker">The comparison freshness capability. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class ComparisonCheckFreshnessHandler(
    IGitComparisonFreshnessChecker comparisonFreshnessChecker,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public static string Action => ComparisonActionConstants.CheckFreshnessAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ComparisonCheckFreshnessParameters>(request.Parameters, Action);
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

        var result = freshnessResult.Data!.State switch
        {
            ComparisonFreshnessState.Current =>
                Result.Success<ComparisonFreshnessResult>(new CurrentComparisonFreshnessResult()),
            ComparisonFreshnessState.Stale =>
                Result.Success<ComparisonFreshnessResult>(new StaleComparisonFreshnessResult()),
            _ => OperationError.InternalError(
                "The comparison freshness state is not approved for the engine protocol.",
                ComparisonErrorCode.UnmappedFreshnessState),
        };
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }
}
