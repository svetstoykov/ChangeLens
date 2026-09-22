using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Helpers;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.Protocol.Helpers;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.AnalysisRuns.Handlers;

/// <summary>
///     Handles the action that polls the current summary of one analysis run.
/// </summary>
internal sealed class AnalysisPollRunHandler(IAnalysisRunCoordinator coordinator, IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    private static readonly OperationError UnreadableReadingModel = OperationError.InternalError(
        "The stored reading model of the completed run is not readable.", AnalysisProtocolErrorCode.UnreadableReadingModel);

    /// <summary>Gets the protocol action handled by this instance.</summary>
    public static string Action => AnalysisActionConstants.PollRunAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<AnalysisPollRunParameters>(request.Parameters, Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        if (!Guid.TryParse(parametersResult.Data!.RunId, out var runId))
        {
            return ProtocolResponseFactory.FromError(request.RequestId,
                OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun));
        }

        var detailResult = await coordinator.PollRunAsync(runId, cancellationToken);
        if (detailResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, detailResult.Errors);
        }

        var detail = detailResult.Data!;
        ReadingModelResult? readingModel = null;
        IReadOnlyList<ValidationRemovalResult>? validationRemovals = null;
        if (detail.State is AnalysisRunState.Completed or AnalysisRunState.CompletedWithLimitations)
        {
            var projectionResult = await coordinator.GetReadingProjectionAsync(runId, cancellationToken);
            if (projectionResult.IsFailure)
            {
                return ProtocolResponseFactory.CreateError(request.RequestId, projectionResult.Errors);
            }

            if (projectionResult.Data is { } projection)
            {
                var readingModelResult = protocolSerializer.DeserializeDocument<ReadingModelResult>(
                    projection.ReadingModelJson, UnreadableReadingModel);
                if (readingModelResult.IsFailure)
                {
                    return ProtocolResponseFactory.CreateError(request.RequestId, readingModelResult.Errors);
                }

                var removalsResult = protocolSerializer.DeserializeDocument<List<ValidationRemovalResult>>(
                    projection.ValidationRemovalsJson, UnreadableReadingModel);
                if (removalsResult.IsFailure)
                {
                    return ProtocolResponseFactory.CreateError(request.RequestId, removalsResult.Errors);
                }

                readingModel = readingModelResult.Data;
                validationRemovals = removalsResult.Data;
            }
        }

        var mappedResult = AnalysisRunSummaryMapper.ToProtocol(detail, readingModel, validationRemovals);
        return mappedResult.IsFailure
            ? ProtocolResponseFactory.CreateError(request.RequestId, mappedResult.Errors)
            : ProtocolResponseFactory.FromResult(request.RequestId, Result.Success(mappedResult.Data));
    }
}
