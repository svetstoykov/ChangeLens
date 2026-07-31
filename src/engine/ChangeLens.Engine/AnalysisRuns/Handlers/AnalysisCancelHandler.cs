using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.AnalysisRuns.Handlers;

/// <summary>
///     Handles the action that requests cancellation of one analysis run.
/// </summary>
internal sealed class AnalysisCancelHandler(IAnalysisRunCoordinator coordinator, IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <summary>Gets the protocol action handled by this instance.</summary>
    public static string Action => AnalysisActionConstants.CancelAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<AnalysisCancelParameters>(request.Parameters, Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        if (!Guid.TryParse(parametersResult.Data!.RunId, out var runId))
        {
            return ProtocolResponseFactory.FromError(
                request.RequestId,
                OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun));
        }

        var result = await coordinator.CancelAsync(runId, cancellationToken);
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }
}
