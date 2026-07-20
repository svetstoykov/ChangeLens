namespace ChangeLens.Engine.Protocol.Models;

internal sealed record ProtocolResultResponse<T>(
    int ProtocolVersion,
    string Type,
    string RequestId,
    T Result);
