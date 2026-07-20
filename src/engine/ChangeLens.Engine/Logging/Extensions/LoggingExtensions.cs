using ChangeLens.Engine.EngineInformation.Constants;
using ChangeLens.Engine.Logging.Constants;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace ChangeLens.Engine.Logging.Extensions;

/// <summary>
///     Provides host-builder extensions for engine logging.
/// </summary>
internal static class LoggingExtensions
{
    /// <summary>
    ///     Configures injectable logging for standard error and rolling local files.
    /// </summary>
    /// <remarks>
    ///     Standard output remains reserved for engine protocol messages. Console diagnostics are always
    ///     routed to standard error, including events below the warning level.
    /// </remarks>
    /// <param name="builder">
    ///     The host application builder to configure. Cannot be <see langword="null" />.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="builder" /> is <see langword="null" />.
    /// </exception>
    internal static void AddEngineLogging(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.ClearProviders();

        var logDirectory = builder.Configuration[EngineLoggingConstants.FileDirectoryConfigurationKey];

        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                EngineLoggingConstants.DefaultApplicationDirectoryName,
                EngineLoggingConstants.DefaultLogDirectoryName);
        }

        builder.Services.AddSerilog(
            (_, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty(
                    EngineLoggingConstants.ApplicationPropertyName,
                    EngineInformationConstants.EngineName)
                .WriteTo.Console(
                    standardErrorFromLevel: LogEventLevel.Verbose,
                    theme: AnsiConsoleTheme.Code,
                    applyThemeToRedirectedOutput: true,
                    outputTemplate: EngineLoggingConstants.ConsoleOutputTemplate)
                .WriteTo.File(
                    Path.Combine(logDirectory, EngineLoggingConstants.LogFileNamePattern),
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: EngineLoggingConstants.LogFileSizeLimitBytes,
                    retainedFileCountLimit: EngineLoggingConstants.RetainedLogFileCount,
                    shared: true,
                    outputTemplate: EngineLoggingConstants.FileOutputTemplate));
    }
}
