namespace ChangeLens.Engine.Protocol.Models;

/// <summary>
///     Represents a failed engine protocol action.
/// </summary>
/// <param name="ProtocolVersion">The protocol version used for the response.</param>
/// <param name="Type">The response type. This value is <c>error</c>.</param>
/// <param name="RequestId">
///     The request identifier, or <see langword="null" /> when it could not be read from the request.
/// </param>
/// <param name="Errors">The non-empty ordered collection of errors returned for the action.</param>
internal sealed record ProtocolErrorResponse(
    int ProtocolVersion,
    string Type,
    string? RequestId,
    IReadOnlyList<ProtocolError> Errors) : ProtocolResponse(ProtocolVersion, Type, RequestId);
