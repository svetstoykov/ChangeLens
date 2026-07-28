using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Diagnostics.Interfaces;
using ChangeLens.Core.Diagnostics.Services;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.EngineStatus.Services;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Engine.Comparisons.Interfaces;
using ChangeLens.Engine.Comparisons.Services;
using ChangeLens.Engine.Logging.Extensions;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Preferences.Interfaces;
using ChangeLens.Engine.Preferences.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Engine.Repositories.Interfaces;
using ChangeLens.Engine.Repositories.Services;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.LocalState.Interfaces;
using ChangeLens.Infrastructure.LocalState.Services;
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
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<ILocalStateDatabase>(
            serviceProvider =>
                new SqliteLocalStateDatabase(
                    builder.Configuration[
                        "ChangeLens:LocalState:Directory"],
                    serviceProvider.GetRequiredService<
                        Microsoft.Extensions.Logging.ILogger<SqliteLocalStateDatabase>>()));
        builder.Services.AddSingleton<ILocalStateInitializer>(
            serviceProvider => serviceProvider.GetRequiredService<ILocalStateDatabase>() as ILocalStateInitializer
                ?? throw new InvalidOperationException("The local-state database must provide lifecycle initialization."));
        builder.Services.AddSingleton<IRepositoryHistoryStore, SqliteRepositoryHistoryStore>();
        builder.Services.AddSingleton<
            IColorThemePreferenceStore,
            SqliteColorThemePreferenceStore>();
        builder.Services.AddSingleton<
            ICanonicalRepositoryPathKeyProvider,
            CanonicalRepositoryPathKeyProvider>();
        builder.Services.AddSingleton<IEngineStatusService, EngineStatusService>();
        builder.Services.AddSingleton<IPathSanitizer, PathSanitizer>();
        builder.Services.AddSingleton<IRepositoryPathResolver, PhysicalRepositoryPathResolver>();
        builder.Services.AddSingleton<IGitCommandRunner>(
            serviceProvider =>
            {
                var configuredExecutable =
                    builder.Configuration[
                        RepositoryInspectionConfigurationConstants.GitExecutableConfigurationKey];
                var executable = string.IsNullOrWhiteSpace(configuredExecutable)
                    ? RepositoryInspectionConfigurationConstants.DefaultGitExecutable
                    : configuredExecutable;
                return new GitCliCommandRunner(
                    executable,
                    [],
                    serviceProvider.GetRequiredService<
                        Microsoft.Extensions.Logging.ILogger<GitCliCommandRunner>>());
            });
        builder.Services.AddSingleton<IGitRepositoryInspector, GitRepositoryInspector>();
        builder.Services.AddSingleton<IComparisonFileSummaryComposer, ComparisonFileSummaryComposer>();
        builder.Services.AddSingleton<IGitComparisonTargetDiscovery, GitComparisonTargetDiscovery>();
        builder.Services.AddSingleton<IGitComparisonPreparer, GitComparisonPreparer>();
        builder.Services.AddSingleton<IGitComparisonFreshnessChecker, GitComparisonFreshnessChecker>();
        builder.Services.AddSingleton<IGitRemoteBaselineTracker, GitRemoteBaselineTracker>();
        builder.Services.AddSingleton<IEngineProtocolSerializer, EngineProtocolSerializer>();
        builder.Services.AddSingleton<IComparisonTargetPageBuilder, ComparisonTargetPageBuilder>();
        builder.Services.AddSingleton<IRepositoryHistoryService, RepositoryHistoryService>();
        builder.Services.AddSingleton<IColorThemePreferenceService, ColorThemePreferenceService>();
        builder.Services.AddSingleton<IEngineProtocolTransport, EngineProtocolTransport>();
        builder.Services.AddSingleton<IEngineActionProcessor, EngineActionProcessor>();
        builder.Services.AddHostedService<EngineProtocolHost>();
    }
}
