namespace ChangeLens.Engine.Protocol.Models;

internal sealed record ProtocolError(
    string Code,
    string Message);
