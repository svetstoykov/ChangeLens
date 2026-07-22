using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Serializes and deserializes versioned engine protocol messages.
/// </summary>
/// <remarks>
///     The host registers this stateless service as a singleton. It owns the protocol's JSON policy and is safe to use
///     concurrently.
/// </remarks>
internal sealed class EngineProtocolSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    /// <summary>
    ///     Deserializes one complete request line into its common protocol envelope.
    /// </summary>
    /// <param name="requestLine">The complete request line. Cannot be <see langword="null" />.</param>
    /// <returns>The deserialized request or its known failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="requestLine" /> is <see langword="null" />.
    /// </exception>
    internal Result<EngineProtocolRequest> DeserializeRequest(string requestLine)
    {
        ArgumentNullException.ThrowIfNull(requestLine);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(requestLine);
        }
        catch (JsonException)
        {
            return Result.Fail<EngineProtocolRequest>(
                OperationError.MalformedInput(
                    "The request is not valid JSON.",
                    EngineErrorCode.InvalidJson));
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
                    return InvalidRequest<EngineProtocolRequest>();
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
                return InvalidRequest<EngineProtocolRequest>();
            }
        }
    }

    /// <summary>
    ///     Deserializes an action's parameter object into its concrete parameter type.
    /// </summary>
    /// <typeparam name="TParameters">The action parameter type.</typeparam>
    /// <param name="parameters">The JSON object containing the action parameters.</param>
    /// <param name="action">The fixed protocol action. Cannot be <see langword="null" />.</param>
    /// <returns>The typed parameters or a validation failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="action" /> is <see langword="null" />.
    /// </exception>
    internal Result<TParameters> DeserializeParameters<TParameters>(JsonElement parameters, string action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            var value = parameters.Deserialize<TParameters>(SerializerOptions);

            return value is null
                ? InvalidParameters<TParameters>(action)
                : Result.Success(value);
        }
        catch (JsonException)
        {
            return InvalidParameters<TParameters>(action);
        }
    }

    /// <summary>
    ///     Serializes a protocol response using its concrete runtime type.
    /// </summary>
    /// <param name="response">The response to serialize. Cannot be <see langword="null" />.</param>
    /// <returns>The serialized protocol response.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="response" /> is <see langword="null" />.
    /// </exception>
    internal Result<string> SerializeResponse(ProtocolResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        try
        {
            return Result.Success<string>(
                JsonSerializer.Serialize(response, response.GetType(), SerializerOptions));
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return Result.Fail<string>(
                OperationError.InternalError(
                    "The engine could not serialize the protocol response.",
                    EngineErrorCode.SerializationFailed));
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
    ///     Creates the standard failure for a request that does not match the protocol schema.
    /// </summary>
    /// <typeparam name="T">The expected deserialized value type.</typeparam>
    /// <returns>A validation failure with the stable invalid-request code.</returns>
    private static Result<T> InvalidRequest<T>() =>
        Result.Fail<T>(
            OperationError.Validation(
                "The request does not match the engine protocol schema.",
                EngineErrorCode.InvalidRequest));

    /// <summary>
    ///     Creates the standard failure for parameters that do not match an action schema.
    /// </summary>
    /// <typeparam name="T">The expected parameter type.</typeparam>
    /// <param name="action">The fixed protocol action.</param>
    /// <returns>A validation failure with the stable invalid-request code.</returns>
    private static Result<T> InvalidParameters<T>(string action) =>
        Result.Fail<T>(
            OperationError.Validation(
                $"The parameters do not match the {action} schema.",
                EngineErrorCode.InvalidRequest));
}
