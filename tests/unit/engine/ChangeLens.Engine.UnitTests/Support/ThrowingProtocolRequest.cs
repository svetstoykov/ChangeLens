using System.Text.Json;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Provides a protocol request that throws when dispatch reads its version.
/// </summary>
internal sealed class ThrowingProtocolRequest : IEngineProtocolRequest
{
    /// <inheritdoc />
    public int ProtocolVersion => throw new InvalidOperationException("sensitive fixture detail");

    /// <inheritdoc />
    public string RequestId => "request-throw";

    /// <inheritdoc />
    public string Method => EngineProtocolConstants.GetInformationMethod;

    /// <inheritdoc />
    public JsonElement? Params => throw new InvalidOperationException("sensitive fixture detail");
}
