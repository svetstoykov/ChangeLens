using System.Diagnostics;
using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.EngineInformation.Models;
using ChangeLens.Engine.EngineInformation.Services;
using ChangeLens.Engine.Hosting.Constants;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Provides the hosted newline-delimited JSON protocol boundary for the engine process.
/// </summary>
/// <remarks>
///     <para>
///         The Generic Host owns this singleton service. It processes requests sequentially and does not need to be thread-safe.
///     </para>
///     <para>
///         Protocol messages use the configured output, while diagnostics use the injected logger. Unexpected action failures are sanitized
///         so that later requests can still be processed. Serialization, output, and lifecycle failures stop the service and fail the
///         process.
///     </para>
/// </remarks>
/// <param name="input">The text stream that supplies protocol requests. Cannot be <see langword="null" />.</param>
/// <param name="output">The text stream that receives protocol responses. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">
///     The serializer for versioned protocol messages. Cannot be <see langword="null" />.
/// </param>
/// <param name="engineInformationProvider">
///     The provider for the engine-information action. Cannot be <see langword="null" />.
/// </param>
/// <param name="logger">The logger for protocol-boundary diagnostics. Cannot be <see langword="null" />.</param>
/// <param name="applicationLifetime">
///     The application lifetime used to stop the engine when protocol input closes. Cannot be <see langword="null" />.
/// </param>
internal sealed class EngineProtocolHost(
    TextReader input,
    TextWriter output,
    EngineProtocolSerializer protocolSerializer,
    EngineInformationProvider engineInformationProvider,
    ILogger<EngineProtocolHost> logger,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    /// <inheritdoc />
    /// <remarks>
    ///     Reads one request per line and writes and flushes exactly one response for each line. Closing the input stops the application. A
    ///     lifecycle failure is logged, sets a nonzero process exit code, and then stops the application.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Engine protocol host started and is awaiting standard input.");

            while (await input.ReadLineAsync(stoppingToken) is { } requestLine)
            {
                await ProcessRequestAsync(requestLine, stoppingToken);
            }

            logger.LogInformation("Engine protocol host stopped after standard input closed.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Engine protocol host stopped after application shutdown was requested.");
        }
        catch (Exception exception)
        {
            Environment.ExitCode = EngineProcessConstants.UnexpectedFailureExitCode;
            logger.LogCritical(exception, EngineProcessConstants.UnexpectedTerminationLogMessage);
        }
        finally
        {
            applicationLifetime.StopApplication();
        }
    }

    /// <summary>
    ///     Processes one request line and writes its response.
    /// </summary>
    /// <param name="requestLine">The complete request line. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while writing the response.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">
    ///     <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    private async Task ProcessRequestAsync(string requestLine, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        logger.LogDebug("Received protocol request from stdin: {ProtocolRequest}", requestLine);

        var requestResult = protocolSerializer.DeserializeRequest(requestLine);
        var request = requestResult.Data;
        var response = requestResult.IsFailure
            ? ProtocolResponseFactory.CreateError(null, requestResult.Errors)
            : DispatchSafely(request!);

        var responseLine = protocolSerializer.SerializeResponse(response);

        await output.WriteLineAsync(responseLine.AsMemory(), cancellationToken);
        await output.FlushAsync(cancellationToken);

        logger.LogDebug("Wrote protocol response to stdout: {ProtocolResponse}", responseLine);

        LogCompletedResponse(response, request, Stopwatch.GetElapsedTime(startedAt));
    }

    /// <summary>
    ///     Dispatches one validated request and sanitizes an unexpected exception from the selected action.
    /// </summary>
    /// <param name="request">The validated request to dispatch. Cannot be <see langword="null" />.</param>
    /// <returns>The action response or a sanitized unexpected-failure response.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    internal ProtocolResponse DispatchSafely(IEngineProtocolRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            return DispatchRequest(request);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected failure processing protocol request {RequestId} for {Method} with error {ErrorCode} in " +
                "{ElapsedMilliseconds:0.000} ms.",
                request.RequestId,
                request.Method,
                EngineProtocolConstants.UnexpectedFailureErrorCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return ProtocolResponseFactory.CreateUnexpectedFailure(request.RequestId);
        }
    }

    /// <summary>
    ///     Dispatches a concrete request to its approved capability entry point.
    /// </summary>
    /// <param name="request">The validated request to dispatch. Cannot be <see langword="null" />.</param>
    /// <returns>The action response.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    internal ProtocolResponse DispatchRequest(IEngineProtocolRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProtocolVersion != EngineProtocolConstants.CurrentVersion)
        {
            return ProtocolResponseFactory.CreateError(
                request.RequestId,
                [
                    OperationError.UnprocessableInput(
                        $"Protocol version {request.ProtocolVersion} is not supported.",
                        EngineProtocolConstants.UnsupportedVersionErrorCode),
                ]);
        }

        return request.Method switch
        {
            EngineProtocolConstants.GetInformationMethod => ExecuteGetInformation(request),

            _ => ProtocolResponseFactory.CreateError(
                request.RequestId,
                [
                    OperationError.NotFound(
                        $"The method '{request.Method}' is not recognized.",
                        EngineProtocolConstants.UnknownMethodErrorCode),
                ]),
        };
    }

    /// <summary>
    ///     Deserializes and executes the engine-information action parameters.
    /// </summary>
    /// <param name="request">The common request envelope. Cannot be <see langword="null" />.</param>
    /// <returns>The engine-information response or parameter-validation error response.</returns>
    private ProtocolResponse ExecuteGetInformation(IEngineProtocolRequest request)
    {
        if (request.Params is null || request.Params.Value.ValueKind == JsonValueKind.Null)
        {
            return ProtocolResponseFactory.CreateWithValue(
                request.RequestId,
                engineInformationProvider.GetInformation(
                    new EngineInformationParameters(),
                    EngineProtocolConstants.CurrentVersion));
        }

        if (request.Params.Value.ValueKind != JsonValueKind.Object)
        {
            return ProtocolResponseFactory.CreateError(
                request.RequestId,
                [
                    OperationError.Validation(
                        "The request params must be a JSON object.",
                        EngineProtocolConstants.InvalidRequestErrorCode),
                ]);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<EngineInformationParameters>(
            request.Params.Value,
            request.Method);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var information = engineInformationProvider.GetInformation(
            parametersResult.Data!,
            EngineProtocolConstants.CurrentVersion);

        return ProtocolResponseFactory.CreateWithValue(request.RequestId, information);
    }

    /// <summary>
    ///     Logs a completed request with its correlation data, error codes, and elapsed time.
    /// </summary>
    /// <param name="response">The response that was written to protocol output.</param>
    /// <param name="request">The validated request, or <see langword="null" /> when the input was rejected.</param>
    /// <param name="elapsed">The time spent processing and writing the response.</param>
    private void LogCompletedResponse(ProtocolResponse response, IEngineProtocolRequest? request, TimeSpan elapsed)
    {
        if (response is ProtocolErrorResponse errorResponse)
        {
            var errorCodes = errorResponse.Errors.Select(error => error.Code).ToArray();

            if (errorCodes.Contains(EngineProtocolConstants.UnexpectedFailureErrorCode, StringComparer.Ordinal))
            {
                return;
            }

            if (request is null)
            {
                logger.LogInformation(
                    "Rejected protocol input {RequestId} with errors {ErrorCodes} in {ElapsedMilliseconds:0.000} ms.",
                    errorResponse.RequestId,
                    errorCodes,
                    elapsed.TotalMilliseconds);
                return;
            }

            logger.LogInformation(
                "Processed protocol request {RequestId} for {Method} with errors {ErrorCodes} in {ElapsedMilliseconds:0.000} ms.",
                errorResponse.RequestId,
                request.Method,
                errorCodes,
                elapsed.TotalMilliseconds);
            return;
        }

        logger.LogInformation(
            "Processed protocol request {RequestId} for {Method} with a result in {ElapsedMilliseconds:0.000} ms.",
            request!.RequestId,
            request.Method,
            elapsed.TotalMilliseconds);
    }
}
