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
    ///     The safe message returned for an unexpected Engine action failure.
    /// </summary>
    internal const string UnexpectedFailureMessage =
        "The engine could not complete the action because of an unexpected failure.";
}
