namespace ChangeLens.Engine.Protocol.Constants;

/// <summary>
///     Provides the stable error codes emitted by the engine protocol.
/// </summary>
internal static class EngineErrorCode
{
    /// <summary>
    ///     The request is not valid JSON.
    /// </summary>
    internal const string InvalidJson = "protocol.invalidJson";

    /// <summary>
    ///     The request does not match the protocol schema.
    /// </summary>
    internal const string InvalidRequest = "protocol.invalidRequest";

    /// <summary>
    ///     The request exceeds the transport size limit.
    /// </summary>
    internal const string RequestTooLarge = "protocol.requestTooLarge";

    /// <summary>
    ///     The protocol version is not supported.
    /// </summary>
    internal const string UnsupportedVersion = "protocol.unsupportedVersion";

    /// <summary>
    ///     The requested action is not recognized.
    /// </summary>
    internal const string UnknownAction = "protocol.unknownAction";

    /// <summary>
    ///     The protocol response could not be serialized.
    /// </summary>
    internal const string SerializationFailed = "protocol.serializationFailed";

    /// <summary>
    ///     Protocol input could not be read.
    /// </summary>
    internal const string ReadFailed = "protocol.readFailed";

    /// <summary>
    ///     Protocol output could not be written.
    /// </summary>
    internal const string WriteFailed = "protocol.writeFailed";

    /// <summary>
    ///     The engine action failed unexpectedly.
    /// </summary>
    internal const string UnexpectedFailure = "engine.unexpectedFailure";
}
