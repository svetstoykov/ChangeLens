using System.Text.Json;
using ChangeLens.Engine.EngineInformation.Services;
using ChangeLens.Engine.Protocol.Models;
using EngineInformationModel = ChangeLens.Engine.EngineInformation.Models.EngineInformation;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Provides the newline-delimited JSON protocol boundary for the engine process.
/// </summary>
/// <remarks>
///     The host writes protocol messages only to its configured output. Diagnostics must use a separate stream.
/// </remarks>
/// <param name="input">The text stream that supplies protocol requests. Cannot be <see langword="null" />.</param>
/// <param name="output">The text stream that receives protocol responses. Cannot be <see langword="null" />.</param>
internal sealed class EngineProtocolHost(
    TextReader input,
    TextWriter output)
{
    private const int CurrentProtocolVersion = 1;
    private readonly EngineInformationProvider _engineInformationProvider = new();
    private readonly TextReader _input = input;
    private readonly TextWriter _output = output;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

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
        while (await _input.ReadLineAsync(cancellationToken) is { } requestLine)
        {
            var response = CreateResponse(requestLine);
            var responseLine = JsonSerializer.Serialize(response, _serializerOptions);

            await _output.WriteLineAsync(responseLine.AsMemory(), cancellationToken);
            await _output.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    ///     Creates the protocol response for a single newline-delimited JSON request.
    /// </summary>
    /// <remarks>
    ///     Valid JSON that does not match the request schema is reported separately from malformed JSON.
    /// </remarks>
    /// <param name="requestLine">The complete protocol request line. Cannot be <see langword="null" />.</param>
    /// <returns>The result or error response to serialize for the request.</returns>
    private object CreateResponse(string requestLine)
    {
        JsonDocument requestDocument;

        try
        {
            requestDocument = JsonDocument.Parse(requestLine);
        }
        catch (JsonException)
        {
            return CreateError(null, "protocol.invalidJson", "The request is not valid JSON.");
        }

        using (requestDocument)
        {
            if (!TryCreateRequest(
                    requestDocument.RootElement,
                    out var request,
                    out var requestId))
            {
                return CreateError(
                    requestId,
                    "protocol.invalidRequest",
                    "The request does not match the engine protocol schema.");
            }

            if (request.ProtocolVersion != CurrentProtocolVersion)
            {
                return CreateError(
                    request.RequestId,
                    "protocol.unsupportedVersion",
                    $"Protocol version {request.ProtocolVersion} is not supported.");
            }

            if (!string.Equals(request.Method, "engine.getInfo", StringComparison.Ordinal))
            {
                return CreateError(
                    request.RequestId,
                    "protocol.unknownMethod",
                    $"The method '{request.Method}' is not recognized.");
            }

            return new ProtocolResultResponse<EngineInformationModel>(
                CurrentProtocolVersion,
                "result",
                request.RequestId,
                _engineInformationProvider.GetInformation());
        }
    }

    /// <summary>
    ///     Creates a request model when the JSON object exactly matches the required request shape.
    /// </summary>
    /// <remarks>
    ///     Rejects missing, unknown, duplicate, and incorrectly typed properties before semantic dispatch.
    /// </remarks>
    /// <param name="root">The JSON value to validate.</param>
    /// <param name="request">The validated request when this method returns <see langword="true" />.</param>
    /// <param name="requestId">
    ///     The valid request identifier when one can be recovered for error correlation; otherwise,
    ///     <see langword="null" />.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> if the JSON object matches the request shape; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool TryCreateRequest(
        JsonElement root,
        out ProtocolRequest request,
        out string? requestId)
    {
        request = null!;
        requestId = null;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
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
            return false;
        }

        request = new ProtocolRequest(protocolVersion, requestId, method);
        return true;
    }

    private static ProtocolErrorResponse CreateError(
        string? requestId,
        string code,
        string message)
    {
        return new ProtocolErrorResponse(
            CurrentProtocolVersion,
            "error",
            requestId,
            new ProtocolError(code, message));
    }
}
