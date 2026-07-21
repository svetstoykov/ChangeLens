using System.Text.Json;

namespace ChangeLens.Engine.Protocol.Models;

/// <summary>
///     Defines the common engine protocol request envelope.
/// </summary>
internal interface IEngineProtocolRequest
{
    /// <summary>
    ///     Gets the protocol version expected by the caller.
    /// </summary>
    int ProtocolVersion { get; }

    /// <summary>
    ///     Gets the identifier used to correlate the request and response.
    /// </summary>
    string RequestId { get; }

    /// <summary>
    ///     Gets the fixed protocol method selected for the action.
    /// </summary>
    string Method { get; }

    /// <summary>
    ///     Gets the JSON value containing the action parameters, or <see langword="null" /> when the action has no payload.
    /// </summary>
    JsonElement? Params { get; }
}
