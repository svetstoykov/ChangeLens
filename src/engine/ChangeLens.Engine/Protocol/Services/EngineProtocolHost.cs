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

    private object CreateResponse(string requestLine)
    {
        ProtocolRequest? request;

        try
        {
            request = JsonSerializer.Deserialize<ProtocolRequest>(requestLine, _serializerOptions);
        }
        catch (JsonException)
        {
            return CreateError(null, "protocol.invalidJson", "The request is not valid JSON.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.RequestId))
        {
            return CreateError(null, "protocol.invalidRequest", "The request identifier is required.");
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
