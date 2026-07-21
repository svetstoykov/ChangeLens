using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// <param name="requestSerializer">
///     The deserializer that maps protocol requests to actions. Cannot be <see langword="null" />.
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
    EngineProtocolRequestSerializer requestSerializer,
    EngineInformationProvider engineInformationProvider,
    ILogger<EngineProtocolHost> logger,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

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

        var requestResult = requestSerializer.Deserialize(requestLine);
        var request = requestResult.Data;
        var actionResult = requestResult.IsFailure
            ? Result.ErrorFromResult<object?>(requestResult)
            : DispatchSafely(request!);
        var requestId = request?.RequestId;
        var response = ProtocolResponseFactory.FromResult(requestId, actionResult);

        var responseLine = JsonSerializer.Serialize(response, response.GetType(), SerializerOptions);

        await output.WriteLineAsync(responseLine.AsMemory(), cancellationToken);
        await output.FlushAsync(cancellationToken);

        logger.LogDebug("Wrote protocol response to stdout: {ProtocolResponse}", responseLine);

        LogCompletedResponse(response, request, Stopwatch.GetElapsedTime(startedAt));
    }

    /// <summary>
    ///     Dispatches one validated request and sanitizes an unexpected exception from the selected action.
    /// </summary>
    /// <param name="request">The validated request to dispatch. Cannot be <see langword="null" />.</param>
    /// <returns>The action result or a sanitized unexpected-failure result.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    internal Result<object?> DispatchSafely(IEngineProtocolRequest request)
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

            return Result.Fail<object?>(
                OperationError.InternalError(
                    EngineProtocolConstants.UnexpectedFailureMessage,
                    EngineProtocolConstants.UnexpectedFailureErrorCode));
        }
    }

    /// <summary>
    ///     Dispatches a concrete request to its approved capability entry point.
    /// </summary>
    /// <param name="request">The validated request to dispatch. Cannot be <see langword="null" />.</param>
    /// <returns>The action result or its known failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    internal Result<object?> DispatchRequest(IEngineProtocolRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProtocolVersion != EngineProtocolConstants.CurrentVersion)
        {
            return Result.Fail<object?>(
                OperationError.UnprocessableInput(
                    $"Protocol version {request.ProtocolVersion} is not supported.",
                    EngineProtocolConstants.UnsupportedVersionErrorCode));
        }

        return request.Method switch
        {
            EngineProtocolConstants.GetInformationMethod => ExecuteGetInformation(request),

            _ => Result.Fail<object?>(
                OperationError.NotFound(
                    $"The method '{request.Method}' is not recognized.",
                    EngineProtocolConstants.UnknownMethodErrorCode)),
        };
    }

    /// <summary>
    ///     Deserializes and executes the engine-information action parameters.
    /// </summary>
    /// <param name="request">The common request envelope. Cannot be <see langword="null" />.</param>
    /// <returns>The engine-information result or parameter-validation error.</returns>
    private Result<object?> ExecuteGetInformation(IEngineProtocolRequest request)
    {
        if (request.Params is null || request.Params.Value.ValueKind == JsonValueKind.Null)
        {
            return Result.Success<object?>(
                engineInformationProvider.GetInformation(
                    new EngineInformationParameters(),
                    EngineProtocolConstants.CurrentVersion));
        }

        if (request.Params.Value.ValueKind != JsonValueKind.Object)
        {
            return Result.Fail<object?>(
                OperationError.Validation(
                    "The request params must be a JSON object.",
                    EngineProtocolConstants.InvalidRequestErrorCode));
        }

        EngineInformationParameters parameters;

        try
        {
            parameters = request.Params.Value.Deserialize<EngineInformationParameters>(SerializerOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return Result.Fail<object?>(
                OperationError.Validation(
                    "The params do not match the engine.getInfo schema.",
                    EngineProtocolConstants.InvalidRequestErrorCode));
        }

        return Result.Success<object?>(
            engineInformationProvider.GetInformation(parameters, EngineProtocolConstants.CurrentVersion));
    }

    /// <summary>
    ///     Creates protocol serialization options for the current protocol version.
    /// </summary>
    /// <returns>Options that serialize error categories as their stable string names.</returns>
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        options.AllowDuplicateProperties = false;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.PropertyNameCaseInsensitive = false;
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        return options;
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
