namespace ChangeLens.Engine.Protocol.Models;

/// <summary>
///     Represents a failed engine protocol request.
/// </summary>
/// <param name="ProtocolVersion">The protocol version used for the response.</param>
/// <param name="Type">The response type. This value is <c>error</c>.</param>
/// <param name="RequestId">
///     The request identifier, or <see langword="null" /> when it could not be read from the request.
/// </param>
/// <param name="Error">The error returned for the request.</param>
internal sealed record ProtocolErrorResponse(
    int ProtocolVersion,
    string Type,
    string? RequestId,
    ProtocolError Error);
