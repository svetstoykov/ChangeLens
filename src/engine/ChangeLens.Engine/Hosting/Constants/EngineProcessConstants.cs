namespace ChangeLens.Engine.Hosting.Constants;

/// <summary>
///     Provides stable values used to control the engine process boundary.
/// </summary>
internal static class EngineProcessConstants
{
    /// <summary>
    ///     The application name used by hosting and logging.
    /// </summary>
    internal const string ApplicationName = "ChangeLens.Engine";

    /// <summary>
    ///     The process exit code used when the engine terminates unexpectedly.
    /// </summary>
    internal const int UnexpectedFailureExitCode = 1;

    /// <summary>
    ///     The log message recorded when the engine terminates unexpectedly.
    /// </summary>
    internal const string UnexpectedTerminationLogMessage = "The engine terminated unexpectedly.";
}
