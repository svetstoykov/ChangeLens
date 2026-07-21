using System.Diagnostics;
using System.Text.Json;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.EngineStatus.Constants;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
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
/// <param name="logger">The logger for action outcomes. Cannot be <see langword="null" />.</param>
internal sealed class EngineActionProcessor(
    IEngineStatusService engineStatusService,
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

        try
        {
            var response = request.ProtocolVersion == EngineProtocolConstants.CurrentVersion
                ? await ProcessKnownVersionAsync(request, cancellationToken)
                : ProtocolResponseFactory.FromResult(
                    request.RequestId,
                    Result.Fail(
                        OperationError.UnprocessableInput(
                            $"Protocol version {request.ProtocolVersion} is not supported.",
                            EngineProtocolConstants.UnsupportedVersionErrorCode)));

            LogOutcome(response, request, Stopwatch.GetElapsedTime(startedAt));
            return response;
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
                EngineProtocolConstants.UnexpectedFailureErrorCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return ProtocolResponseFactory.CreateUnexpectedFailure(request.RequestId);
        }
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
            EngineStatusActionConstants.CheckStatusAction
                when request.Parameters.ValueKind == JsonValueKind.Undefined =>
                ProcessCheckStatusAsync(request, cancellationToken),
            EngineStatusActionConstants.CheckStatusAction =>
                Task.FromResult<ProtocolResponse>(
                    ProtocolResponseFactory.FromResult(
                        request.RequestId,
                        Result.Fail(
                            OperationError.Validation(
                                "The engine.checkStatus action does not accept parameters.",
                                EngineProtocolConstants.InvalidRequestErrorCode)))),
            _ => Task.FromResult<ProtocolResponse>(
                ProtocolResponseFactory.FromResult(
                    request.RequestId,
                    Result.Fail(
                        OperationError.NotFound(
                            $"The action '{request.Action}' is not recognized.",
                            EngineProtocolConstants.UnknownActionErrorCode)))),
        };

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
