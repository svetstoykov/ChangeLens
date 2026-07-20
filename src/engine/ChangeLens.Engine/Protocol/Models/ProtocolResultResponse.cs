namespace ChangeLens.Engine.Protocol.Models;

/// <summary>
///     Represents a successful engine protocol request.
/// </summary>
/// <typeparam name="T">The type of result returned by the engine operation.</typeparam>
/// <param name="ProtocolVersion">The protocol version used for the response.</param>
/// <param name="Type">The response type. This value is <c>result</c>.</param>
/// <param name="RequestId">The identifier of the completed request.</param>
/// <param name="Result">The value returned by the engine operation.</param>
internal sealed record ProtocolResultResponse<T>(
    int ProtocolVersion,
    string Type,
    string RequestId,
    T Result);
