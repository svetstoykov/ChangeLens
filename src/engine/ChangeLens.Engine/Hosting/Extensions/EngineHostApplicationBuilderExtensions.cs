using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Services;
using ChangeLens.Engine.Comparisons.Handlers;
using ChangeLens.Engine.Comparisons.Interfaces;
using ChangeLens.Engine.Comparisons.Services;
using ChangeLens.Engine.EngineStatus.Handlers;
using ChangeLens.Engine.Logging.Extensions;
using ChangeLens.Engine.Preferences.Handlers;
using ChangeLens.Engine.Protocol.Constants;
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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

        builder.ConfigureContainer(
            new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }),
            static _ => { });
        builder.AddEngineLogging();

        AddRuntimeServices(builder);
        AddLocalStateServices(builder);
        AddPreferenceServices(builder);
        AddEngineStatusServices(builder);
        AddRepositoryServices(builder);
        AddComparisonServices(builder);
        AddProtocolServices(builder);
        AddActionHandlers(builder);
        ValidateActionHandlerRegistrations(builder.Services);
    }

    /// <summary>Validates that action-handler registrations exactly match the approved action list.</summary>
    /// <param name="services">The service descriptors to validate. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The approved actions are blank or duplicated, or handler registrations are unkeyed, keyed by a non-string
    ///     or blank value, unapproved, missing, or duplicated.
    /// </exception>
    internal static void ValidateActionHandlerRegistrations(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var issues = new List<string>();
        var approvedActions = EngineActionConstants.ApprovedActions;
        var validApprovedActions = approvedActions
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .ToHashSet(StringComparer.Ordinal);

        issues.AddRange(
            approvedActions
                .Select((action, index) => (action, index))
                .Where(item => string.IsNullOrWhiteSpace(item.action))
                .Select(item => $"approved action at index {item.index} is blank"));
        issues.AddRange(
            approvedActions
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .GroupBy(action => action, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"approved action '{group.Key}' appears {group.Count()} times"));

        var registrationsByApprovedAction = validApprovedActions.ToDictionary(action => action, _ => 0, StringComparer.Ordinal);
        foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IActionHandler)))
        {
            var registration = DescribeActionHandlerRegistration(descriptor);
            if (!descriptor.IsKeyedService)
            {
                issues.Add($"handler registration {registration} is unkeyed");
                continue;
            }

            if (descriptor.ServiceKey is not string action)
            {
                issues.Add(
                    $"handler registration {registration} has non-string key " +
                    $"'{descriptor.ServiceKey?.GetType().FullName ?? "<null>"}'");
                continue;
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                issues.Add($"handler registration {registration} has a blank action key");
                continue;
            }

            if (!validApprovedActions.Contains(action))
            {
                issues.Add($"handler registration {registration} uses unapproved action '{action}'");
                continue;
            }

            registrationsByApprovedAction[action]++;
        }

        issues.AddRange(
            registrationsByApprovedAction
                .Where(pair => pair.Value == 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"approved action '{pair.Key}' has no handler registration"));
        issues.AddRange(
            registrationsByApprovedAction
                .Where(pair => pair.Value > 1)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"approved action '{pair.Key}' has {pair.Value} handler registrations"));

        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "Action-handler registration validation failed: " + string.Join("; ", issues) + ".");
        }
    }

    /// <summary>Registers process-owned runtime services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    private static void AddRuntimeServices(HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<TextReader>(_ => Console.In);
        builder.Services.AddSingleton<TextWriter>(_ => Console.Out);
        builder.Services.AddSingleton(TimeProvider.System);
    }

    /// <summary>Registers local-state persistence and repository-history services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    private static void AddLocalStateServices(HostApplicationBuilder builder)
    {
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
    private static void AddPreferenceServices(HostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IColorThemePreferenceStore, SqliteColorThemePreferenceStore>();
        builder.Services.AddScoped<IColorThemePreferenceService, ColorThemePreferenceService>();
    }

    /// <summary>Registers engine readiness services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    private static void AddEngineStatusServices(HostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IEngineStatusService, EngineStatusService>();
    }

    /// <summary>Registers repository inspection services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    private static void AddRepositoryServices(HostApplicationBuilder builder)
    {
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
    private static void AddComparisonServices(HostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IComparisonFileSummaryComposer, ComparisonFileSummaryComposer>();
        builder.Services.AddScoped<IGitComparisonTargetDiscovery, GitComparisonTargetDiscovery>();
        builder.Services.AddScoped<IGitComparisonPreparer, GitComparisonPreparer>();
        builder.Services.AddScoped<IGitComparisonFreshnessChecker, GitComparisonFreshnessChecker>();
        builder.Services.AddScoped<IGitRemoteBaselineTracker, GitRemoteBaselineTracker>();
        builder.Services.AddScoped<IComparisonTargetPageBuilder, ComparisonTargetPageBuilder>();
    }

    /// <summary>Registers singleton protocol transport services and the protocol host.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    private static void AddProtocolServices(HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IEngineProtocolSerializer, EngineProtocolSerializer>();
        builder.Services.AddSingleton<IEngineProtocolTransport, EngineProtocolTransport>();
        builder.Services.AddHostedService<EngineProtocolHost>();
    }

    /// <summary>Registers every approved action handler as a keyed scoped service.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    private static void AddActionHandlers(HostApplicationBuilder builder)
    {
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
    }

    /// <summary>Registers one action handler under its declared action.</summary>
    /// <typeparam name="THandler">The action-handler implementation to register.</typeparam>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    private static void AddActionHandler<THandler>(HostApplicationBuilder builder)
        where THandler : class, IActionHandler =>
        builder.Services.AddKeyedScoped(typeof(IActionHandler), THandler.Action, typeof(THandler));

    private static string DescribeActionHandlerRegistration(ServiceDescriptor descriptor)
    {
        var implementationType = descriptor.IsKeyedService
            ? descriptor.KeyedImplementationType
            : descriptor.ImplementationType;
        return $"'{implementationType?.FullName ?? "<factory or instance>"}'";
    }
}
