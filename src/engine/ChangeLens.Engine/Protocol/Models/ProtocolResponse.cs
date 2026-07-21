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
    int ProtocolVersion,
    string Type,
    string? RequestId);
