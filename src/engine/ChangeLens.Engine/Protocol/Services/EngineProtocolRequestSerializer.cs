using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Deserializes newline-delimited JSON requests into common protocol envelopes.
/// </summary>
/// <remarks>
///     The host registers this stateless service as a singleton. It is safe to use concurrently.
/// </remarks>
internal sealed class EngineProtocolRequestSerializer
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
    internal Result<IEngineProtocolRequest> Deserialize(string requestLine)
    {
        ArgumentNullException.ThrowIfNull(requestLine);

        try
        {
            var request = JsonSerializer.Deserialize<EngineProtocolRequest>(requestLine, SerializerOptions);

            return request is null || string.IsNullOrWhiteSpace(request.RequestId)
                ? InvalidRequest()
                : Result.Success<IEngineProtocolRequest>(request);
        }
        catch (JsonException)
        {
            return InvalidRequest();
        }
    }

    /// <summary>
    ///     Creates strict serializer options for incoming protocol requests.
    /// </summary>
    /// <returns>Options that reject missing, unknown, duplicate, nullable, and incorrectly typed request properties.</returns>
    private static JsonSerializerOptions CreateSerializerOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            AllowDuplicateProperties = false,
            NumberHandling = JsonNumberHandling.Strict,
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

    /// <summary>
    ///     Creates the standard failure for a request that does not match the protocol schema.
    /// </summary>
    /// <returns>A validation failure with the stable invalid-request code.</returns>
    private static Result<IEngineProtocolRequest> InvalidRequest() =>
        Result.Fail<IEngineProtocolRequest>(
            OperationError.Validation(
                "The request does not match the engine protocol schema.",
                EngineProtocolConstants.InvalidRequestErrorCode));
}
