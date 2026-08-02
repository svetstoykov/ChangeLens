using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.AnalysisRuns.Services;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.AnalysisRuns.Handlers;

/// <summary>
///     Handles the action that looks up the active analysis run for a repository.
/// </summary>
internal sealed class AnalysisGetActiveHandler(IAnalysisRunCoordinator coordinator, IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <summary>Gets the protocol action handled by this instance.</summary>
    public static string Action => AnalysisActionConstants.GetActiveAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<AnalysisGetActiveParameters>(request.Parameters, Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var activeResult = await coordinator.GetActiveAsync(parametersResult.Data!.Path, cancellationToken);
        if (activeResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(request.RequestId, Result.ErrorFromResult<AnalysisGetActiveResult>(activeResult));
        }

        if (activeResult.Data is null)
        {
            return ProtocolResponseFactory.FromResult(request.RequestId, Result.Success<AnalysisGetActiveResult>(new NoneAnalysisGetActiveResult()));
        }

        var mappedResult = AnalysisRunSummaryMapper.ToProtocol(activeResult.Data);
        if (mappedResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, mappedResult.Errors);
        }

        return ProtocolResponseFactory.FromResult(request.RequestId,
            Result.Success<AnalysisGetActiveResult>(new ActiveAnalysisGetActiveResult(mappedResult.Data!)));
    }
}
