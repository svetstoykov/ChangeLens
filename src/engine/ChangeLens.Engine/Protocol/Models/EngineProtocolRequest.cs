using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Protocol.Models;

/// <summary>
///     Represents the common engine protocol request envelope.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EngineProtocolRequest : IEngineProtocolRequest
{
    /// <summary>
    ///     Gets the protocol version expected by the caller.
    /// </summary>
    [JsonRequired]
    public int ProtocolVersion { get; init; }

    /// <summary>
    ///     Gets the identifier used to correlate the request and response.
    /// </summary>
    [JsonRequired]
    public string RequestId { get; init; } = null!;

    /// <summary>
    ///     Gets the fixed protocol method selected for the action.
    /// </summary>
    [JsonRequired]
    public string Method { get; init; } = null!;

    /// <summary>
    ///     Gets the JSON value containing the action parameters, or <see langword="null" /> when absent.
    /// </summary>
    public JsonElement? Params { get; init; }
}
