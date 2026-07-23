using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.EngineStatus.Services;
using ChangeLens.Core.Git.Services;
using ChangeLens.Engine.Logging.Extensions;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.Git.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Hosting.Extensions;

/// <summary>
///     Provides engine-specific composition for the Generic Host builder.
/// </summary>
internal static class EngineHostApplicationBuilderExtensions
{
    /// <summary>
    ///     Adds the engine protocol boundary and its supporting services to the host builder.
    /// </summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="builder" /> is <see langword="null" />.
    /// </exception>
    internal static void AddEngine(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddEngineLogging();
        builder.Services.AddSingleton<TextReader>(_ => Console.In);
        builder.Services.AddSingleton<TextWriter>(_ => Console.Out);
        builder.Services.AddSingleton<IEngineStatusService, EngineStatusService>();
        builder.Services.AddSingleton<PhysicalRepositoryPathResolver>();
        builder.Services.AddSingleton(
            _ =>
            {
                var configuredExecutable =
                    builder.Configuration[
                        RepositoryInspectionConfigurationConstants.GitExecutableConfigurationKey];
                var executable = string.IsNullOrWhiteSpace(configuredExecutable)
                    ? RepositoryInspectionConfigurationConstants.DefaultGitExecutable
                    : configuredExecutable;
                return new GitCliCommandRunner(executable, []);
            });
        builder.Services.AddSingleton(
            serviceProvider =>
                new GitRepositoryInspector(
                    serviceProvider.GetRequiredService<GitCliCommandRunner>(),
                    serviceProvider.GetRequiredService<PhysicalRepositoryPathResolver>()));
        builder.Services.AddSingleton<EngineProtocolSerializer>();
        builder.Services.AddSingleton<IEngineProtocolTransport, EngineProtocolTransport>();
        builder.Services.AddSingleton<EngineActionProcessor>();
        builder.Services.AddHostedService<EngineProtocolHost>();
    }
}
