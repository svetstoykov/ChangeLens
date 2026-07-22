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
    ///     Gets the protocol version used by the request.
    /// </summary>
    [JsonRequired]
    public int ProtocolVersion { get; init; }

    /// <summary>
    ///     Gets the identifier used to correlate the request with its response.
    /// </summary>
    [JsonRequired]
    public string RequestId { get; init; } = null!;

    /// <summary>
    ///     Gets the action requested from the engine.
    /// </summary>
    [JsonRequired]
    public string Action { get; init; } = null!;

    /// <summary>
    ///     Gets the intentionally unbound parameters value retained at the protocol boundary.
    /// </summary>
    /// <remarks>
    ///     The protocol slice interprets this value only after selecting <see cref="Action" />. An undefined value
    ///     means the property was omitted, while every other JSON value means it was supplied. The value must not
    ///     escape the Engine protocol slice.
    /// </remarks>
    public JsonElement Parameters { get; init; }
}
