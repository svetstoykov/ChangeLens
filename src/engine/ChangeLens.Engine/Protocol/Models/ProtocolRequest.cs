namespace ChangeLens.Engine.Protocol.Models;

/// <summary>
///     Represents one request read from the engine protocol input.
/// </summary>
/// <param name="ProtocolVersion">The protocol version expected by the caller.</param>
/// <param name="RequestId">The identifier used to match the request and response.</param>
/// <param name="Method">The engine operation to run.</param>
internal sealed record ProtocolRequest(
    int ProtocolVersion,
    string RequestId,
    string Method);
