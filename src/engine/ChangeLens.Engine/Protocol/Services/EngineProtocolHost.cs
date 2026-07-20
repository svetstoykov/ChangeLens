using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.EngineInformation.Services;
using ChangeLens.Engine.Protocol.Models;
using Microsoft.Extensions.Logging;
using EngineInformationModel = ChangeLens.Engine.EngineInformation.Models.EngineInformation;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Provides the newline-delimited JSON protocol boundary for the engine process.
/// </summary>
/// <remarks>
///     <para>
///         The host writes protocol messages only to its configured output. Diagnostics use the injected logger.
///     </para>
///     <para>
///         This service is registered as a singleton. It is not thread-safe and supports one active
///         <see cref="RunAsync" /> operation at a time.
///     </para>
/// </remarks>
/// <param name="input">The text stream that supplies protocol requests. Cannot be <see langword="null" />.</param>
/// <param name="output">The text stream that receives protocol responses. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for protocol-boundary diagnostics. Cannot be <see langword="null" />.</param>
internal sealed class EngineProtocolHost(
    TextReader input,
    TextWriter output,
    ILogger<EngineProtocolHost> logger)
{
    private const int CurrentProtocolVersion = 1;
    private readonly EngineInformationProvider _engineInformationProvider = new();
    private readonly TextReader _input = input;
    private readonly ILogger<EngineProtocolHost> _logger = logger;
    private readonly TextWriter _output = output;
    private readonly JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

    /// <summary>
    ///     Asynchronously processes protocol requests until the input ends or cancellation is requested.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Reads one newline-delimited JSON request at a time and writes and flushes exactly one protocol
    ///         response for each request.
    ///     </para>
    ///     <para>
    ///         The configured output is reserved for protocol messages and is not used for diagnostics.
    ///     </para>
    /// </remarks>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">
    ///     If the <see cref="CancellationToken" /> is canceled.
    /// </exception>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Engine protocol host started and is awaiting standard input.");

        while (await _input.ReadLineAsync(cancellationToken) is { } requestLine)
        {
            var startedAt = Stopwatch.GetTimestamp();

            _logger.LogDebug(
                "Received protocol request from stdin: {ProtocolRequest}",
                requestLine);

            var response = CreateResponse(requestLine, out var request);
            var responseLine = JsonSerializer.Serialize(response, _serializerOptions);

            await _output.WriteLineAsync(responseLine.AsMemory(), cancellationToken);
            await _output.FlushAsync(cancellationToken);

            _logger.LogDebug(
                "Wrote protocol response to stdout: {ProtocolResponse}",
                responseLine);
            LogCompletedResponse(response, request, Stopwatch.GetElapsedTime(startedAt));
        }

        _logger.LogInformation("Engine protocol host stopped after standard input closed.");
    }

    /// <summary>
    ///     Creates the protocol response for a single newline-delimited JSON request.
    /// </summary>
    /// <remarks>
    ///     Valid JSON that does not match the request schema is reported separately from malformed JSON.
    /// </remarks>
    /// <param name="requestLine">The complete protocol request line. Cannot be <see langword="null" />.</param>
    /// <param name="request">
    ///     The validated request when validation succeeds; otherwise, <see langword="null" />.
    /// </param>
    /// <returns>The result or error response to serialize for the request.</returns>
    private object CreateResponse(
        string requestLine,
        out ProtocolRequest? request)
    {
        request = null;
        JsonDocument requestDocument;

        try
        {
            requestDocument = JsonDocument.Parse(requestLine);
        }
        catch (JsonException)
        {
            return CreateError(
                null,
                OperationError.MalformedInput(
                    "The request is not valid JSON.",
                    "protocol.invalidJson"));
        }

        using (requestDocument)
        {
            var requestResult = CreateRequest(requestDocument.RootElement, out var requestId);

            if (requestResult.IsFailure)
            {
                return CreateError(requestId, requestResult.Errors[0]);
            }

            request = requestResult.Data!;
            var operationResult = DispatchRequest(request);

            if (operationResult.IsFailure)
            {
                return CreateError(request.RequestId, operationResult.Errors[0]);
            }

            return new ProtocolResultResponse<EngineInformationModel>(
                CurrentProtocolVersion,
                "result",
                request.RequestId,
                operationResult.Data!);
        }
    }

    /// <summary>
    ///     Creates a result for a JSON request after validating its required shape.
    /// </summary>
    /// <remarks>
    ///     Rejects missing, unknown, duplicate, and incorrectly typed properties before semantic dispatch.
    /// </remarks>
    /// <param name="root">The JSON value to validate.</param>
    /// <param name="requestId">
    ///     The valid request identifier when one can be recovered for error correlation; otherwise,
    ///     <see langword="null" />.
    /// </param>
    /// <returns>The validated request, or a failure that describes why the request is invalid.</returns>
    private static Result<ProtocolRequest> CreateRequest(
        JsonElement root,
        out string? requestId)
    {
        requestId = null;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return InvalidRequest();
        }

        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        var hasValidProperties = true;

        foreach (var property in root.EnumerateObject())
        {
            var isKnownProperty = property.Name is "protocolVersion" or "requestId" or "method";
            hasValidProperties &= isKnownProperty && propertyNames.Add(property.Name);
        }

        if (root.TryGetProperty("requestId", out var requestIdElement) &&
            requestIdElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(requestIdElement.GetString()))
        {
            requestId = requestIdElement.GetString();
        }

        if (!hasValidProperties ||
            !root.TryGetProperty("protocolVersion", out var protocolVersionElement) ||
            protocolVersionElement.ValueKind != JsonValueKind.Number ||
            !protocolVersionElement.TryGetInt32(out var protocolVersion) ||
            requestId is null ||
            !root.TryGetProperty("method", out var methodElement) ||
            methodElement.ValueKind != JsonValueKind.String ||
            methodElement.GetString() is not { } method)
        {
            return InvalidRequest();
        }

        return Result.Success(new ProtocolRequest(protocolVersion, requestId, method));
    }

    /// <summary>
    ///     Dispatches a validated protocol request to its operation result.
    /// </summary>
    /// <param name="request">The validated request to dispatch. Cannot be <see langword="null" />.</param>
    /// <returns>The requested engine information, or a known dispatch failure.</returns>
    private Result<EngineInformationModel> DispatchRequest(ProtocolRequest request)
    {
        if (request.ProtocolVersion != CurrentProtocolVersion)
        {
            return Result.Fail<EngineInformationModel>(
                OperationError.UnprocessableInput(
                    $"Protocol version {request.ProtocolVersion} is not supported.",
                    "protocol.unsupportedVersion"));
        }

        if (!string.Equals(request.Method, "engine.getInfo", StringComparison.Ordinal))
        {
            return Result.Fail<EngineInformationModel>(
                OperationError.NotFound(
                    $"The method '{request.Method}' is not recognized.",
                    "protocol.unknownMethod"));
        }

        return Result.Success(_engineInformationProvider.GetInformation());
    }

    /// <summary>
    ///     Creates the standard failure for a request that does not match the protocol schema.
    /// </summary>
    /// <returns>A validation failure with the stable invalid-request code.</returns>
    private static Result<ProtocolRequest> InvalidRequest() =>
        Result.Fail<ProtocolRequest>(
            OperationError.Validation(
                "The request does not match the engine protocol schema.",
                "protocol.invalidRequest"));

    /// <summary>
    ///     Creates protocol serialization options for the current protocol version.
    /// </summary>
    /// <returns>Options that serialize error categories as their stable string names.</returns>
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>
    ///     Creates an error response from a known operation failure.
    /// </summary>
    /// <param name="requestId">The request identifier, or <see langword="null" /> when it is unavailable.</param>
    /// <param name="error">The operation error to expose through the protocol. Cannot be <see langword="null" />.</param>
    /// <returns>An error response that preserves the error category, code, and message.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="error" /> is <see langword="null" />.
    /// </exception>
    private static ProtocolErrorResponse CreateError(
        string? requestId,
        OperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ProtocolErrorResponse(
            CurrentProtocolVersion,
            "error",
            requestId,
            new ProtocolError(error.Type, error.Code!, error.Message));
    }

    /// <summary>
    ///     Logs the request outcome with its identifier, method, error code, and elapsed time when available.
    /// </summary>
    /// <remarks>
    ///     <paramref name="response" /> must be a protocol error or an engine-information result.
    /// </remarks>
    /// <param name="response">The response that was written to protocol output.</param>
    /// <param name="request">
    ///     The validated request, or <see langword="null" /> when the input could not be validated.
    /// </param>
    /// <param name="elapsed">The time spent processing and writing the response.</param>
    private void LogCompletedResponse(
        object response,
        ProtocolRequest? request,
        TimeSpan elapsed)
    {
        if (response is ProtocolErrorResponse errorResponse)
        {
            if (request is null)
            {
                _logger.LogInformation(
                    "Rejected protocol input {RequestId} with error {ErrorCode} in {ElapsedMilliseconds:0.000} ms.",
                    errorResponse.RequestId,
                    errorResponse.Error.Code,
                    elapsed.TotalMilliseconds);
                return;
            }

            _logger.LogInformation(
                "Processed protocol request {RequestId} for {Method} with error {ErrorCode} in {ElapsedMilliseconds:0.000} ms.",
                errorResponse.RequestId,
                request.Method,
                errorResponse.Error.Code,
                elapsed.TotalMilliseconds);
            return;
        }

        var resultResponse = (ProtocolResultResponse<EngineInformationModel>)response;

        _logger.LogInformation(
            "Processed protocol request {RequestId} for {Method} with a result in {ElapsedMilliseconds:0.000} ms.",
            resultResponse.RequestId,
            request!.Method,
            elapsed.TotalMilliseconds);
    }
}
