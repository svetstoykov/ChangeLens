namespace ChangeLens.Engine.Protocol.Models;

internal sealed record ProtocolRequest(
    int ProtocolVersion,
    string RequestId,
    string Method);
