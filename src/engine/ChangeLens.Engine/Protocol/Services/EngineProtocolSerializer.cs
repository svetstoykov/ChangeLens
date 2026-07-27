using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Serializes and deserializes versioned engine protocol messages.
/// </summary>
/// <remarks>
///     The host registers this stateless service as a singleton. It owns the protocol's JSON policy and is safe to use
///     concurrently.
/// </remarks>
internal sealed class EngineProtocolSerializer : IEngineProtocolSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private static readonly OperationError InvalidRequestError = OperationError.Validation(
        "The request does not match the engine protocol schema.",
        EngineErrorCode.InvalidRequest);

    private static readonly OperationError SerializationFailedError = OperationError.InternalError(
        "The engine could not serialize the protocol response.",
        EngineErrorCode.SerializationFailed);

    /// <inheritdoc />
    public Result<EngineProtocolRequest> DeserializeRequest(string requestLine)
    {
        ArgumentNullException.ThrowIfNull(requestLine);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(requestLine);
        }
        catch (JsonException)
        {
            return OperationError.MalformedInput(
                "The request is not valid JSON.",
                EngineErrorCode.InvalidJson);
        }

        using (document)
        {
            try
            {
                var request = document.RootElement.Deserialize<EngineProtocolRequest>(SerializerOptions);

                if (request is null ||
                    string.IsNullOrWhiteSpace(request.RequestId) ||
                    string.IsNullOrWhiteSpace(request.Action))
                {
                    return InvalidRequestError;
                }

                return Result.Success(
                    new EngineProtocolRequest
                    {
                        ProtocolVersion = request.ProtocolVersion,
                        RequestId = request.RequestId,
                        Action = request.Action,
                        Parameters = request.Parameters.ValueKind == JsonValueKind.Undefined
                            ? default
                            : request.Parameters.Clone(),
                    });
            }
            catch (JsonException)
            {
                return InvalidRequestError;
            }
        }
    }

    /// <inheritdoc />
    public Result<TParameters> DeserializeParameters<TParameters>(JsonElement parameters, string action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            var value = parameters.Deserialize<TParameters>(SerializerOptions);

            return value is null
                ? InvalidParametersError(action)
                : Result.Success(value);
        }
        catch (JsonException)
        {
            return InvalidParametersError(action);
        }
    }

    /// <inheritdoc />
    public Result<string> SerializeResponse(ProtocolResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        try
        {
            return Result.Success<string>(
                JsonSerializer.Serialize(response, response.GetType(), SerializerOptions));
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return SerializationFailedError;
        }
    }

    /// <inheritdoc />
    public Result<int> GetSerializedUtf8ByteCount(ProtocolResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        try
        {
            return Result.Success(
                JsonSerializer.SerializeToUtf8Bytes(
                    response,
                    response.GetType(),
                    SerializerOptions).Length);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return SerializationFailedError;
        }
    }

    /// <summary>
    ///     Creates strict JSON options for all protocol messages.
    /// </summary>
    /// <returns>Options that enforce the versioned protocol's property and enum representation.</returns>
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowDuplicateProperties = false,
            NumberHandling = JsonNumberHandling.Strict,
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>
    ///     Creates the standard failure for parameters that do not match an action schema.
    /// </summary>
    /// <param name="action">The fixed protocol action.</param>
    /// <returns>A validation failure with the stable invalid-request code.</returns>
    private static OperationError InvalidParametersError(string action) =>
        OperationError.Validation(
            $"The parameters do not match the {action} schema.",
            EngineErrorCode.InvalidRequest);
}
