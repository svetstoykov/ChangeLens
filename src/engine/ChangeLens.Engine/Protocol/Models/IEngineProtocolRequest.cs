namespace ChangeLens.Engine.Protocol.Models;

/// <summary>
///     Defines the metadata shared by every engine protocol request.
/// </summary>
internal interface IEngineProtocolRequest
{
    /// <summary>
    ///     Gets the protocol version used by the request.
    /// </summary>
    int ProtocolVersion { get; }

    /// <summary>
    ///     Gets the identifier used to correlate the request with its response.
    /// </summary>
    string RequestId { get; }

    /// <summary>
    ///     Gets the action requested from the engine.
    /// </summary>
    string Action { get; }
}
