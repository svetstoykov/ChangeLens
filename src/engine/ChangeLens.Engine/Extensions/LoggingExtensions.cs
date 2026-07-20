using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace ChangeLens.Engine.Extensions;

/// <summary>
///     Provides host-builder extensions for engine logging.
/// </summary>
internal static class LoggingExtensions
{
    private const long LogFileSizeLimitBytes = 10 * 1024 * 1024;
    private const int RetainedLogFileCount = 14;

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

        var logDirectory = builder.Configuration["ChangeLens:Logging:FileDirectory"];

        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ChangeLens",
                "Logs");
        }

        builder.Services.AddSerilog(
            (_, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "ChangeLens.Engine")
                .WriteTo.Console(
                    standardErrorFromLevel: LogEventLevel.Verbose,
                    theme: AnsiConsoleTheme.Code,
                    applyThemeToRedirectedOutput: true,
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    Path.Combine(logDirectory, "changelens-engine-.log"),
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: LogFileSizeLimitBytes,
                    retainedFileCountLimit: RetainedLogFileCount,
                    shared: true,
                    outputTemplate:
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));
    }
}
