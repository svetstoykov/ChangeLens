namespace ChangeLens.Engine.Protocol.Constants;

/// <summary>
///     Provides the stable identifiers used by the engine protocol boundary.
/// </summary>
internal static class EngineProtocolConstants
{
    /// <summary>
    ///     The protocol version supported by this engine build.
    /// </summary>
    internal const int CurrentVersion = 1;

    /// <summary>
    ///     The maximum number of content characters accepted in one request line.
    /// </summary>
    internal const int MaximumRequestCharacterCount = 65_536;

    /// <summary>
    ///     The fixed number of characters read from protocol input at once.
    /// </summary>
    internal const int ReadBufferCharacterCount = 4_096;

    /// <summary>
    ///     The message type used for successful responses.
    /// </summary>
    internal const string ResultResponseType = "result";

    /// <summary>
    ///     The message type used for failed responses.
    /// </summary>
    internal const string ErrorResponseType = "error";

    /// <summary>
    ///     The stable error code for syntactically invalid JSON.
    /// </summary>
    internal const string InvalidJsonErrorCode = "protocol.invalidJson";

    /// <summary>
    ///     The stable error code for requests that do not match the protocol schema.
    /// </summary>
    internal const string InvalidRequestErrorCode = "protocol.invalidRequest";

    /// <summary>
    ///     The stable error code for request lines that exceed the transport bound.
    /// </summary>
    internal const string RequestTooLargeErrorCode = "protocol.requestTooLarge";

    /// <summary>
    ///     The stable error code for unsupported protocol versions.
    /// </summary>
    internal const string UnsupportedVersionErrorCode = "protocol.unsupportedVersion";

    /// <summary>
    ///     The stable error code for unrecognized protocol actions.
    /// </summary>
    internal const string UnknownActionErrorCode = "protocol.unknownAction";

    /// <summary>
    ///     The stable error code for response serialization failures.
    /// </summary>
    internal const string SerializationFailedErrorCode = "protocol.serializationFailed";

    /// <summary>
    ///     The stable error code for protocol input failures.
    /// </summary>
    internal const string ReadFailedErrorCode = "protocol.readFailed";

    /// <summary>
    ///     The stable error code for protocol output failures.
    /// </summary>
    internal const string WriteFailedErrorCode = "protocol.writeFailed";

    /// <summary>
    ///     The stable error code returned for an unexpected Engine action failure.
    /// </summary>
    internal const string UnexpectedFailureErrorCode = "engine.unexpectedFailure";

    /// <summary>
    ///     The safe message returned for an unexpected Engine action failure.
    /// </summary>
    internal const string UnexpectedFailureMessage =
        "The engine could not complete the action because of an unexpected failure.";
}
