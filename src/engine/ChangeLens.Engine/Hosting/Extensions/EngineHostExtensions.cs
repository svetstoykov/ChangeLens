using ChangeLens.Engine.EngineInformation.Constants;
using ChangeLens.Engine.Hosting.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Hosting.Extensions;

/// <summary>
///     Provides engine-specific execution for the Generic Host.
/// </summary>
internal static class EngineHostExtensions
{
    /// <summary>
    ///     Asynchronously runs the engine until protocol input closes or application shutdown is requested.
    /// </summary>
    /// <param name="host">The configured engine host. Cannot be <see langword="null" />.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the engine process exit code.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="host" /> is <see langword="null" />.
    /// </exception>
    internal static async Task<int> RunEngineAsync(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var logger = host.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(EngineInformationConstants.EngineName);

        try
        {
            await host.RunAsync();
            return Environment.ExitCode;
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, EngineProcessConstants.UnexpectedTerminationLogMessage);
            return EngineProcessConstants.UnexpectedFailureExitCode;
        }
    }
}
