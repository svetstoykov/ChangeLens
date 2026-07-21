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
///     The host registers this stateless service as a singleton. It owns the protocol's JSON policy and is safe to use concurrently.
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
    internal Result<IEngineProtocolRequest> DeserializeRequest(string requestLine)
    {
        ArgumentNullException.ThrowIfNull(requestLine);

        try
        {
            var request = JsonSerializer.Deserialize<EngineProtocolRequest>(requestLine, SerializerOptions);

            return request is null || string.IsNullOrWhiteSpace(request.RequestId)
                ? InvalidRequest<IEngineProtocolRequest>()
                : Result.Success<IEngineProtocolRequest>(request);
        }
        catch (JsonException)
        {
            return InvalidRequest<IEngineProtocolRequest>();
        }
    }

    /// <summary>
    ///     Deserializes an action's parameter object into its concrete parameter type.
    /// </summary>
    /// <typeparam name="TParameters">The action parameter type.</typeparam>
    /// <param name="parameters">The JSON object containing the action parameters.</param>
    /// <param name="method">The fixed protocol method selected for the action. Cannot be <see langword="null" />.</param>
    /// <returns>The typed parameters or a validation failure.</returns>
    internal Result<TParameters> DeserializeParameters<TParameters>(JsonElement parameters, string method)
    {
        ArgumentNullException.ThrowIfNull(method);

        try
        {
            var value = parameters.Deserialize<TParameters>(SerializerOptions);

            return value is null
                ? InvalidParameters<TParameters>(method)
                : Result.Success(value);
        }
        catch (JsonException)
        {
            return InvalidParameters<TParameters>(method);
        }
    }

    /// <summary>
    ///     Serializes a protocol response using its concrete runtime type.
    /// </summary>
    /// <param name="response">The response to serialize. Cannot be <see langword="null" />.</param>
    /// <returns>The serialized protocol response.</returns>
    internal string SerializeResponse(ProtocolResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(response, response.GetType(), SerializerOptions);
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
                EngineProtocolConstants.InvalidRequestErrorCode));

    /// <summary>
    ///     Creates the standard failure for parameters that do not match an action schema.
    /// </summary>
    /// <typeparam name="T">The expected parameter type.</typeparam>
    /// <param name="method">The fixed protocol method selected for the action.</param>
    /// <returns>A validation failure with the stable invalid-request code.</returns>
    private static Result<T> InvalidParameters<T>(string method) =>
        Result.Fail<T>(
            OperationError.Validation(
                $"The params do not match the {method} schema.",
                EngineProtocolConstants.InvalidRequestErrorCode));
}
