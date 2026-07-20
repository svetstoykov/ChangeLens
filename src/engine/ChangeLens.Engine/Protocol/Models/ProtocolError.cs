namespace ChangeLens.Engine.Protocol.Models;

/// <summary>
///     Represents a known failure returned through the engine protocol.
/// </summary>
/// <param name="Code">The stable code that identifies the failure.</param>
/// <param name="Message">The message that explains the failure.</param>
internal sealed record ProtocolError(
    string Code,
    string Message);
