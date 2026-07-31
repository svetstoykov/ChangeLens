using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Services;
using ChangeLens.Engine.AnalysisRuns.Handlers;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using ChangeLens.Engine.AnalysisRuns.Services;
using ChangeLens.Engine.Comparisons.Handlers;
using ChangeLens.Engine.Comparisons.Interfaces;
using ChangeLens.Engine.Comparisons.Services;
using ChangeLens.Engine.EngineStatus.Handlers;
using ChangeLens.Engine.Preferences.Handlers;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Engine.Repositories.Handlers;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.EngineStatus.Services;
using ChangeLens.Infrastructure.Git.Models;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.LocalState.Services;
using ChangeLens.Infrastructure.AnalysisRuns.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Hosting.Extensions;

/// <summary>
///     Provides one capability registration extension per part of the engine service graph.
/// </summary>
/// <remarks>
///     The engine executable calls these in capability order, so the composed graph stays readable at the process
///     entry point while each capability owns its own registrations here.
/// </remarks>
internal static class EngineHostApplicationBuilderExtensions
{
    /// <summary>Registers process-owned runtime services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddRuntimeServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<TextReader>(_ => Console.In);
        builder.Services.AddSingleton<TextWriter>(_ => Console.Out);
        builder.Services.AddSingleton(TimeProvider.System);
    }

    /// <summary>Registers local-state persistence and repository-history services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddLocalStateServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var paths = LocalStatePaths.Resolve(builder.Configuration[LocalStateConstants.DirectoryConfigurationKey]);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = LocalStateConstants.CommandTimeoutSeconds,
            ForeignKeys = true,
        }.ToString();

        builder.Services.AddSingleton(paths);
        builder.Services.AddDbContext<ChangeLensLocalStateDbContext>(options => options.UseSqlite(connectionString));
        builder.Services.AddScoped<ILocalStateInitializer, SqliteLocalStateInitializer>();
        builder.Services.AddScoped<IRepositoryHistoryStore, SqliteRepositoryHistoryStore>();
        builder.Services.AddScoped<IRepositoryHistoryService, RepositoryHistoryService>();
    }

    /// <summary>Registers color-theme preference services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddPreferenceServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<IColorThemePreferenceStore, SqliteColorThemePreferenceStore>();
        builder.Services.AddScoped<IColorThemePreferenceService, ColorThemePreferenceService>();
    }

    /// <summary>Registers engine readiness services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddEngineStatusServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<IEngineStatusService, EngineStatusService>();
    }

    /// <summary>Registers repository inspection services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddRepositoryServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<ICanonicalRepositoryPathKeyProvider, CanonicalRepositoryPathKeyProvider>();
        builder.Services.AddScoped<IRepositoryPathResolver, PhysicalRepositoryPathResolver>();
        builder.Services.Configure<GitCommandRunnerOptions>(
            options => options.ExecutablePath =
                builder.Configuration[RepositoryInspectionConfigurationConstants.GitExecutableConfigurationKey]);
        builder.Services.AddScoped<IGitCommandRunner, GitCliCommandRunner>();
        builder.Services.AddScoped<IGitRepositoryInspector, GitRepositoryInspector>();
    }

    /// <summary>Registers comparison services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddComparisonServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<IComparisonFileSummaryComposer, ComparisonFileSummaryComposer>();
        builder.Services.AddScoped<IGitComparisonTargetDiscovery, GitComparisonTargetDiscovery>();
        builder.Services.AddScoped<IGitComparisonPreparer, GitComparisonPreparer>();
        builder.Services.AddScoped<IGitComparisonFreshnessChecker, GitComparisonFreshnessChecker>();
        builder.Services.AddScoped<IGitRemoteBaselineTracker, GitRemoteBaselineTracker>();
        builder.Services.AddScoped<IComparisonTargetPageBuilder, ComparisonTargetPageBuilder>();
    }

    /// <summary>Registers analysis run services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddAnalysisRunServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IAnalysisProcessorControl, AnalysisProcessorControl>();
        builder.Services.AddScoped<IAnalysisRunStore, SqliteAnalysisRunStore>();
        builder.Services.AddScoped<IAnalysisPipeline, ShallowAnalysisPipeline>();
        builder.Services.AddScoped<IAnalysisRunCoordinator, AnalysisRunCoordinator>();
    }

    /// <summary>Registers singleton protocol transport services and the protocol host.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddProtocolServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IEngineProtocolSerializer, EngineProtocolSerializer>();
        builder.Services.AddSingleton<IEngineProtocolTransport, EngineProtocolTransport>();
        builder.Services.AddHostedService<EngineProtocolHost>();
    }

    /// <summary>Registers every approved action handler as a keyed scoped service.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddActionHandlers(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddActionHandler<RepositoryOpenHandler>(builder);
        AddActionHandler<RepositoryRestoreLastHandler>(builder);
        AddActionHandler<RepositoryListRecentHandler>(builder);
        AddActionHandler<RepositoryRemoveRecentHandler>(builder);
        AddActionHandler<ComparisonListTargetsHandler>(builder);
        AddActionHandler<ComparisonPrepareHandler>(builder);
        AddActionHandler<ComparisonCheckFreshnessHandler>(builder);
        AddActionHandler<ComparisonCheckRemoteBaselineHandler>(builder);
        AddActionHandler<ComparisonRefreshRemoteBaselineHandler>(builder);
        AddActionHandler<PreferenceGetColorThemeHandler>(builder);
        AddActionHandler<PreferenceSetColorThemeHandler>(builder);
        AddActionHandler<EngineCheckStatusHandler>(builder);
        AddActionHandler<AnalysisStartHandler>(builder);
        AddActionHandler<AnalysisGetActiveHandler>(builder);
        AddActionHandler<AnalysisPollRunHandler>(builder);
        AddActionHandler<AnalysisCancelHandler>(builder);
    }

    /// <summary>Registers one action handler under its declared action.</summary>
    /// <typeparam name="THandler">The action-handler implementation to register.</typeparam>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    private static void AddActionHandler<THandler>(HostApplicationBuilder builder)
        where THandler : class, IActionHandler =>
        builder.Services.AddKeyedScoped(typeof(IActionHandler), THandler.Action, typeof(THandler));
}
