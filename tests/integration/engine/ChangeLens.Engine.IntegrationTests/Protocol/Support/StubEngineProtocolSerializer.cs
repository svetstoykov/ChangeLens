using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Provides one caller-selected typed parameter object without applying production JSON policy.
/// </summary>
/// <param name="deserializedParameters">The typed parameters returned by deserialization.</param>
internal sealed class StubEngineProtocolSerializer(object deserializedParameters) : IEngineProtocolSerializer
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    ///     The controlled protocol serializer does not deserialize request envelopes.
    /// </exception>
    public Result<EngineProtocolRequest> DeserializeRequest(string requestLine) =>
        throw new NotSupportedException("The controlled protocol serializer does not deserialize request envelopes.");

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    ///     The caller requests parameters that are not the configured type.
    /// </exception>
    public Result<TParameters> DeserializeParameters<TParameters>(JsonElement parameters, string action)
    {
        if (deserializedParameters is TParameters value)
        {
            return Result.Success(value);
        }

        throw new InvalidOperationException($"The controlled protocol serializer cannot return {typeof(TParameters).FullName}.");
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    ///     The controlled protocol serializer does not serialize responses.
    /// </exception>
    public Result<string> SerializeResponse(ProtocolResponse response) =>
        throw new NotSupportedException("The controlled protocol serializer does not serialize responses.");

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    ///     The controlled protocol serializer does not measure responses.
    /// </exception>
    public Result<int> GetSerializedUtf8ByteCount(ProtocolResponse response) =>
        throw new NotSupportedException("The controlled protocol serializer does not measure responses.");
}
