using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Protocol.Models;

/// <summary>
///     Represents one correlated result or error written by the engine protocol.
/// </summary>
/// <param name="ProtocolVersion">The protocol version used for the response.</param>
/// <param name="Type">The response discriminator.</param>
/// <param name="RequestId">
///     The request identifier, or <see langword="null" /> when correlation could not be recovered.
/// </param>
internal abstract record ProtocolResponse(
    [property: JsonPropertyOrder(0)] int ProtocolVersion,
    [property: JsonPropertyOrder(1)] string Type,
    [property: JsonPropertyOrder(2)] string? RequestId);
