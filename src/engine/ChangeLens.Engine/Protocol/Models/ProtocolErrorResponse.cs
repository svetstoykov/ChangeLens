namespace ChangeLens.Engine.Protocol.Models;

internal sealed record ProtocolErrorResponse(
    int ProtocolVersion,
    string Type,
    string? RequestId,
    ProtocolError Error);
