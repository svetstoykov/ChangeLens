namespace ChangeLens.Engine.Logging.Constants;

/// <summary>
///     Provides stable configuration and output values for engine logging.
/// </summary>
internal static class EngineLoggingConstants
{
    /// <summary>
    ///     The configuration key that overrides the local log directory.
    /// </summary>
    internal const string FileDirectoryConfigurationKey = "ChangeLens:Logging:FileDirectory";

    /// <summary>
    ///     The default root directory for ChangeLens application data.
    /// </summary>
    internal const string DefaultApplicationDirectoryName = "ChangeLens";

    /// <summary>
    ///     The default directory containing engine log files.
    /// </summary>
    internal const string DefaultLogDirectoryName = "Logs";

    /// <summary>
    ///     The structured property that identifies the logging application.
    /// </summary>
    internal const string ApplicationPropertyName = "Application";

    /// <summary>
    ///     The output template used for standard-error diagnostics.
    /// </summary>
    internal const string ConsoleOutputTemplate =
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    ///     The rolling file name pattern used for local engine logs.
    /// </summary>
    internal const string LogFileNamePattern = "changelens-engine-.log";

    /// <summary>
    ///     The output template used for rolling local log files.
    /// </summary>
    internal const string FileOutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    ///     The maximum size of one rolling log file.
    /// </summary>
    internal const long LogFileSizeLimitBytes = 10 * 1024 * 1024;

    /// <summary>
    ///     The maximum number of rolling log files retained locally.
    /// </summary>
    internal const int RetainedLogFileCount = 14;
}
