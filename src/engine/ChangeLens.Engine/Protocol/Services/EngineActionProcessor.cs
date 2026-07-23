using System.Diagnostics;
using System.Text.Json;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.EngineStatus.Constants;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Engine.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Selects an approved engine action and maps its concrete Core Result to the protocol.
/// </summary>
/// <remarks>
///     The host registers this service as a singleton and processes actions sequentially. The service depends only on
///     singleton-safe collaborators and does not maintain mutable state.
/// </remarks>
/// <param name="engineStatusService">The engine-status capability. Cannot be <see langword="null" />.</param>
/// <param name="gitRepositoryInspector">The Git repository inspection capability. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for action outcomes. Cannot be <see langword="null" />.</param>
internal sealed class EngineActionProcessor(
    IEngineStatusService engineStatusService,
    GitRepositoryInspector gitRepositoryInspector,
    EngineProtocolSerializer protocolSerializer,
    ILogger<EngineActionProcessor> logger)
{
    /// <summary>
    ///     Asynchronously processes one validated common request envelope.
    /// </summary>
    /// <param name="request">The request to process. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the action.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains one correlated protocol
    ///     response.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    internal async Task<ProtocolResponse> ProcessAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startedAt = Stopwatch.GetTimestamp();

        ProtocolResponse response;
        try
        {
            if (request.ProtocolVersion != EngineProtocolConstants.CurrentVersion)
            {
                response = ProtocolResponseFactory.FromError(
                    request.RequestId,
                    OperationError.UnprocessableInput(
                        $"Protocol version {request.ProtocolVersion} is not supported.",
                        EngineErrorCode.UnsupportedVersion));
            }
            else
            {
                response = await this.ProcessKnownVersionAsync(request, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected failure processing engine action {RequestId} for {Action} with error {ErrorCode} in " +
                "{ElapsedMilliseconds:0.000} ms.",
                request.RequestId,
                request.Action,
                EngineErrorCode.UnexpectedFailure,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return ProtocolResponseFactory.CreateUnexpectedFailure(request.RequestId);
        }

        this.LogOutcome(response, request, Stopwatch.GetElapsedTime(startedAt));
        return response;
    }

    /// <summary>
    ///     Selects one action from the current protocol version.
    /// </summary>
    /// <param name="request">The current-version request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the action.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the selected action response.
    /// </returns>
    private Task<ProtocolResponse> ProcessKnownVersionAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken) =>
        request.Action switch
        {
            RepositoryActionConstants.OpenAction =>
                this.ProcessRepositoryOpenAsync(request, cancellationToken),
            EngineStatusActionConstants.CheckStatusAction
                when request.Parameters.ValueKind == JsonValueKind.Undefined =>
                this.ProcessCheckStatusAsync(request, cancellationToken),
            EngineStatusActionConstants.CheckStatusAction => Task.FromResult(
                ProtocolResponseFactory.FromError(
                    request.RequestId,
                    OperationError.Validation(
                        "The engine.checkStatus action does not accept parameters.",
                        EngineErrorCode.InvalidRequest))),
            _ => Task.FromResult(
                ProtocolResponseFactory.FromError(
                    request.RequestId,
                    OperationError.NotFound(
                        $"The action '{request.Action}' is not recognized.",
                        EngineErrorCode.UnknownAction))),
        };

    /// <summary>
    ///     Asynchronously binds and executes the repository-open action.
    /// </summary>
    /// <param name="request">The current-version repository-open request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for repository inspection.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the mapped repository response.
    /// </returns>
    private async Task<ProtocolResponse> ProcessRepositoryOpenAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.FromError(
                request.RequestId,
                OperationError.Validation(
                    "The repositories.open action requires parameters.",
                    EngineErrorCode.InvalidRequest));
        }

        var parametersResult = protocolSerializer.DeserializeParameters<RepositoryOpenParameters>(
            request.Parameters,
            RepositoryActionConstants.OpenAction);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var inspectionResult = await gitRepositoryInspector.InspectAsync(
            parametersResult.Data!.Path,
            cancellationToken);
        if (inspectionResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<RepositoryOpenResult>(inspectionResult));
        }

        return ProtocolResponseFactory.FromResult(
            request.RequestId,
            Result.Success(RepositoryOpenResult.FromDescriptor(inspectionResult.Data!)));
    }

    /// <summary>
    ///     Asynchronously executes the payload-free engine-status action.
    /// </summary>
    /// <param name="request">The validated status request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the status check.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the mapped status response.
    /// </returns>
    private async Task<ProtocolResponse> ProcessCheckStatusAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        var result = await engineStatusService.CheckStatusAsync(cancellationToken);
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }

    /// <summary>
    ///     Logs one successful or expected failed action outcome.
    /// </summary>
    /// <param name="response">The action response.</param>
    /// <param name="request">The processed request.</param>
    /// <param name="elapsed">The elapsed action-processing time.</param>
    private void LogOutcome(
        ProtocolResponse response,
        EngineProtocolRequest request,
        TimeSpan elapsed)
    {
        if (response is ProtocolErrorResponse errorResponse)
        {
            logger.LogInformation(
                "Processed engine action {RequestId} for {Action} with errors {ErrorCodes} in " +
                "{ElapsedMilliseconds:0.000} ms.",
                request.RequestId,
                request.Action,
                errorResponse.Errors.Select(error => error.Code).ToArray(),
                elapsed.TotalMilliseconds);
            return;
        }

        logger.LogInformation(
            "Processed engine action {RequestId} for {Action} with a result in {ElapsedMilliseconds:0.000} ms.",
            request.RequestId,
            request.Action,
            elapsed.TotalMilliseconds);
    }
}
