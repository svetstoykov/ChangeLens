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
    ///     The request property containing the protocol version.
    /// </summary>
    internal const string ProtocolVersionPropertyName = "protocolVersion";

    /// <summary>
    ///     The request property containing the correlation identifier.
    /// </summary>
    internal const string RequestIdPropertyName = "requestId";

    /// <summary>
    ///     The request property containing the operation name.
    /// </summary>
    internal const string MethodPropertyName = "method";

    /// <summary>
    ///     The operation that returns identifying information about the engine.
    /// </summary>
    internal const string GetInformationMethod = "engine.getInfo";

    /// <summary>
    ///     The message type used for successful responses.
    /// </summary>
    internal const string ResultResponseType = "result";

    /// <summary>
    ///     The message type used for failed responses.
    /// </summary>
    internal const string ErrorResponseType = "error";

    /// <summary>
    ///     The stable error code for malformed JSON input.
    /// </summary>
    internal const string InvalidJsonErrorCode = "protocol.invalidJson";

    /// <summary>
    ///     The stable error code for requests that do not match the protocol schema.
    /// </summary>
    internal const string InvalidRequestErrorCode = "protocol.invalidRequest";

    /// <summary>
    ///     The stable error code for unsupported protocol versions.
    /// </summary>
    internal const string UnsupportedVersionErrorCode = "protocol.unsupportedVersion";

    /// <summary>
    ///     The stable error code for unrecognized protocol operations.
    /// </summary>
    internal const string UnknownMethodErrorCode = "protocol.unknownMethod";

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
