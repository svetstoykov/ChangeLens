using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.AnalysisRuns.Handlers;

/// <summary>
///     Handles the action that starts an analysis run.
/// </summary>
internal sealed class AnalysisStartHandler(IAnalysisRunCoordinator coordinator, IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <summary>Gets the protocol action handled by this instance.</summary>
    public static string Action => AnalysisActionConstants.StartAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<AnalysisStartParameters>(request.Parameters, Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var parameters = parametersResult.Data!;
        var outcomeResult = await coordinator.StartAsync(
            parameters.Path,
            parameters.Target,
            parameters.FreshnessToken,
            parameters.ChangeContext,
            cancellationToken);
        if (outcomeResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(request.RequestId, Result.ErrorFromResult<AnalysisStartResult>(outcomeResult));
        }

        AnalysisStartResult mapped = outcomeResult.Data!.Kind switch
        {
            AnalysisStartOutcomeKind.Accepted => new AcceptedAnalysisStartResult(
                outcomeResult.Data.RunId!.Value.ToString(),
                outcomeResult.Data.RequestedAtUnixMilliseconds!.Value),
            AnalysisStartOutcomeKind.RejectedStale => new RejectedStaleAnalysisStartResult(),
            AnalysisStartOutcomeKind.RejectedActive => new RejectedActiveAnalysisStartResult(outcomeResult.Data.ActiveRunId!.Value.ToString()),
            _ => throw new ArgumentOutOfRangeException(nameof(outcomeResult), outcomeResult.Data.Kind, "Unapproved start outcome."),
        };
        return ProtocolResponseFactory.FromResult(request.RequestId, Result.Success(mapped));
    }
}
