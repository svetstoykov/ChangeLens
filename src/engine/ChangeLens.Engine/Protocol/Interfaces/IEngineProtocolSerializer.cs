using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.Protocol.Interfaces;

/// <summary>
///     Defines strict serialization for versioned engine protocol messages.
/// </summary>
internal interface IEngineProtocolSerializer
{
    /// <summary>
    ///     Deserializes one complete request line into its common protocol envelope.
    /// </summary>
    /// <param name="requestLine">The complete request line. Cannot be <see langword="null" />.</param>
    /// <returns>The deserialized request or its known failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="requestLine" /> is <see langword="null" />.
    /// </exception>
    Result<EngineProtocolRequest> DeserializeRequest(string requestLine);

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
    Result<TParameters> DeserializeParameters<TParameters>(JsonElement parameters, string action);

    /// <summary>
    ///     Serializes a protocol response using its concrete runtime type.
    /// </summary>
    /// <param name="response">The response to serialize. Cannot be <see langword="null" />.</param>
    /// <returns>The serialized protocol response.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="response" /> is <see langword="null" />.
    /// </exception>
    Result<string> SerializeResponse(ProtocolResponse response);

    /// <summary>
    ///     Measures a protocol response with the exact production UTF-8 serialization policy.
    /// </summary>
    /// <param name="response">The response to measure. Cannot be <see langword="null" />.</param>
    /// <returns>The exact serialized UTF-8 byte count or a serialization failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="response" /> is <see langword="null" />.
    /// </exception>
    Result<int> GetSerializedUtf8ByteCount(ProtocolResponse response);

    /// <summary>
    ///     Serializes one protocol document, such as a stored reading model, with the production serialization policy.
    /// </summary>
    /// <typeparam name="TDocument">The protocol document type.</typeparam>
    /// <param name="document">The document to serialize. Cannot be <see langword="null" />.</param>
    /// <returns>The serialized document or a serialization failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="document" /> is <see langword="null" />.
    /// </exception>
    Result<string> SerializeDocument<TDocument>(TDocument document)
        where TDocument : class;

    /// <summary>
    ///     Deserializes one protocol document written by <see cref="SerializeDocument{TDocument}" /> with the same strict
    ///     policy.
    /// </summary>
    /// <typeparam name="TDocument">The protocol document type.</typeparam>
    /// <param name="json">The serialized document. Cannot be <see langword="null" />.</param>
    /// <param name="unreadable">The caller-owned error returned when the text is not a readable document.</param>
    /// <returns>The deserialized document, or <paramref name="unreadable" />.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="json" /> or <paramref name="unreadable" /> is <see langword="null" />.
    /// </exception>
    Result<TDocument> DeserializeDocument<TDocument>(string json, OperationError unreadable)
        where TDocument : class;
}
