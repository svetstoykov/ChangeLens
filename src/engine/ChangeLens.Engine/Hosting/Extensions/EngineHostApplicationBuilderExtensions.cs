using System.Globalization;
using ChangeLens.Core.ChangeAnatomy.Interfaces;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ChangeAnatomy.Services;
using ChangeLens.Core.ChangeAnatomy.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Services;
using ChangeLens.Core.Curation.Constants;
using ChangeLens.Core.Curation.Interfaces;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.Curation.Services;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.ContextPolicy.Constants;
using ChangeLens.Core.ContextPolicy.Interfaces;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.ContextPolicy.Services;
using ChangeLens.Core.Correspondence.Constants;
using ChangeLens.Core.Correspondence.Interfaces;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Correspondence.Services;
using ChangeLens.Core.DraftValidation.Interfaces;
using ChangeLens.Core.DraftValidation.Services;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Interfaces;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceBinder.Services;
using ChangeLens.Core.EvidenceGraph.Constants;
using ChangeLens.Core.EvidenceGraph.Interfaces;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.EvidenceGraph.Services;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Services;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Constants;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Core.Snapshots.Services;
using ChangeLens.Engine.AnalysisRuns.Handlers;
using ChangeLens.Engine.AnalysisRuns.Hosting;
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
using ChangeLens.Infrastructure.ModelCompletion.Constants;
using ChangeLens.Infrastructure.ModelCompletion.Extensions;
using ChangeLens.Infrastructure.ModelCompletion.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        builder.Services.AddScoped<GitCliCommandRunner>();
        builder.Services.AddScoped<IGitCommandRunner>(services => services.GetRequiredService<GitCliCommandRunner>());
        builder.Services.AddScoped<IGitBinaryCommandRunner>(services => services.GetRequiredService<GitCliCommandRunner>());
        builder.Services.AddScoped<IGitRepositoryInspector, GitRepositoryInspector>();
    }

    /// <summary>Reads the configured bounds for frozen Git tree access.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <returns>The configured bounds, using safe defaults for absent or malformed values.</returns>
    private static FrozenGitTreeReaderOptions CreateFrozenGitTreeReaderOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = new FrozenGitTreeReaderOptions();
        return new FrozenGitTreeReaderOptions
        {
            MaximumBlobBytes = ReadPositiveInt(
                configuration, FrozenGitTreeReaderConfigurationConstants.MaximumBlobBytesKey, defaults.MaximumBlobBytes),
            MaximumTreeFiles = ReadPositiveInt(
                configuration, FrozenGitTreeReaderConfigurationConstants.MaximumTreeFilesKey, defaults.MaximumTreeFiles),
            MaximumHistoryCommits = ReadPositiveInt(
                configuration, FrozenGitTreeReaderConfigurationConstants.MaximumHistoryCommitsKey, defaults.MaximumHistoryCommits),
            MaximumHistoryPathsPerCommit = ReadPositiveInt(
                configuration, FrozenGitTreeReaderConfigurationConstants.MaximumHistoryPathsPerCommitKey,
                defaults.MaximumHistoryPathsPerCommit),
            CommandTimeout = ReadPositiveTimeSpan(
                configuration, FrozenGitTreeReaderConfigurationConstants.CommandTimeoutKey, defaults.CommandTimeout),
        };
    }

    /// <summary>Reads the configured bounds for deterministic change anatomy.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <returns>The configured anatomy bounds, using safe defaults for absent or malformed values.</returns>
    private static ChangeAnatomyOptions CreateChangeAnatomyOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = new ChangeAnatomyOptions();
        return new ChangeAnatomyOptions
        {
            MinimumKeyLength = ReadPositiveInt(configuration, ChangeAnatomyConfigurationConstants.MinimumKeyLengthKey,
                defaults.MinimumKeyLength),
            MaximumKeysPerFile = ReadPositiveInt(configuration, ChangeAnatomyConfigurationConstants.MaximumKeysPerFileKey,
                defaults.MaximumKeysPerFile),
            MaximumOccurrencesPerKey = ReadPositiveInt(configuration, ChangeAnatomyConfigurationConstants.MaximumOccurrencesPerKeyKey,
                defaults.MaximumOccurrencesPerKey),
        };
    }

    /// <summary>Reads the configured bounds and weights for correspondence ranking.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <returns>The configured correspondence options, using safe defaults for absent or malformed values.</returns>
    private static CorrespondenceOptions CreateCorrespondenceOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = new CorrespondenceOptions();
        return new CorrespondenceOptions
        {
            MaximumCandidates = ReadPositiveInt(configuration, CorrespondenceConfigurationConstants.MaximumCandidatesKey, defaults.MaximumCandidates),
            MaximumIndexedKeysPerFile = ReadPositiveInt(
                configuration, CorrespondenceConfigurationConstants.MaximumIndexedKeysPerFileKey, defaults.MaximumIndexedKeysPerFile),
            MaximumReasonsPerCandidate = ReadPositiveInt(
                configuration, CorrespondenceConfigurationConstants.MaximumReasonsPerCandidateKey, defaults.MaximumReasonsPerCandidate),
            IncludeCoChange = ReadBoolean(configuration, CorrespondenceConfigurationConstants.IncludeCoChangeKey, defaults.IncludeCoChange),
        };
    }

    /// <summary>Reads the configured bounds for evidence graph construction.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <returns>The configured evidence graph options, using safe defaults for absent or malformed values.</returns>
    private static EvidenceGraphOptions CreateEvidenceGraphOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = new EvidenceGraphOptions();
        return new EvidenceGraphOptions
        {
            MaximumQuoteWindowNodes = ReadPositiveInt(
                configuration, EvidenceGraphConfigurationConstants.MaximumQuoteWindowNodesKey, defaults.MaximumQuoteWindowNodes),
            MaximumEdges = ReadPositiveInt(configuration, EvidenceGraphConfigurationConstants.MaximumEdgesKey, defaults.MaximumEdges),
        };
    }

    /// <summary>Reads the configured bounds for context policy disclosure.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <returns>The configured context policy options, using safe defaults for absent or malformed values.</returns>
    private static ContextPolicyOptions CreateContextPolicyOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = new ContextPolicyOptions();
        return new ContextPolicyOptions
        {
            MaximumDisclosedCharactersPerNode = ReadPositiveInt(
                configuration, ContextPolicyConfigurationConstants.MaximumDisclosedCharactersPerNodeKey, defaults.MaximumDisclosedCharactersPerNode),
        };
    }

    /// <summary>Reads the configured evidence binder limits.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <returns>The configured binder limits, using safe defaults for absent or malformed values.</returns>
    private static EvidenceBinderOptions CreateEvidenceBinderOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = new EvidenceBinderOptions();
        return new EvidenceBinderOptions
        {
            ContextWindowTokens = ReadPositiveInt(
                configuration, EvidenceBinderConfigurationConstants.ContextWindowTokensKey, defaults.ContextWindowTokens),
            CuratorOutputCharacters = ReadNonNegativeInt(
                configuration, EvidenceBinderConfigurationConstants.CuratorOutputCharactersKey, defaults.CuratorOutputCharacters),
            PromptReserveCharacters = ReadNonNegativeInt(
                configuration, EvidenceBinderConfigurationConstants.PromptReserveCharactersKey, defaults.PromptReserveCharacters),
            MaximumBinderCharacters = ReadOptionalPositiveInt(
                configuration, EvidenceBinderConfigurationConstants.MaximumBinderCharactersKey),
            TargetUtilization = ReadFraction(configuration, EvidenceBinderConfigurationConstants.TargetUtilizationKey, defaults.TargetUtilization),
        };
    }

    /// <summary>Reads the configured curator call limits.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <returns>The configured curator limits, using safe defaults for absent or malformed values.</returns>
    private static CuratorOptions CreateCuratorOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = new CuratorOptions();
        return new CuratorOptions
        {
            MaximumOutputCharacters = ReadPositiveInt(
                configuration, CuratorConfigurationConstants.MaximumOutputCharactersKey, defaults.MaximumOutputCharacters),
            MaximumOutputTokens = ReadPositiveInt(
                configuration, CuratorConfigurationConstants.MaximumOutputTokensKey, defaults.MaximumOutputTokens),
            ReasoningEffort = Enum.TryParse<ModelReasoningEffort>(
                configuration[CuratorConfigurationConstants.ReasoningEffortKey], true, out var reasoningEffort)
                ? reasoningEffort
                : defaults.ReasoningEffort,
        };
    }

    /// <summary>Reads the configured OpenAI-compatible model completion provider settings.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <returns>The provider settings, which may omit credentials so calls can fail at call time.</returns>
    private static ModelCompletionOptions CreateModelCompletionOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = new ModelCompletionOptions();
        return new ModelCompletionOptions
        {
            BaseUrl = configuration[ModelCompletionConfigurationConstants.BaseUrlKey],
            Model = configuration[ModelCompletionConfigurationConstants.ModelKey],
            ApiKey = configuration[ModelCompletionConfigurationConstants.ApiKeyKey],
            RequestTimeout = ReadPositiveTimeSpan(
                configuration, ModelCompletionConfigurationConstants.RequestTimeoutKey, defaults.RequestTimeout),
        };
    }

    /// <summary>Reads one positive integer configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The positive configured value or <paramref name="fallback" />.</returns>
    private static int ReadPositiveInt(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;

    /// <summary>Reads a non-negative integer configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The configured value or <paramref name="fallback" />.</returns>
    private static int ReadNonNegativeInt(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], out var value) && value >= 0 ? value : fallback;

    /// <summary>Reads an optional positive integer configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <returns>The configured value, or <see langword="null" /> when absent or invalid.</returns>
    private static int? ReadOptionalPositiveInt(IConfiguration configuration, string key) =>
        int.TryParse(configuration[key], out var value) && value > 0 ? value : null;

    /// <summary>Reads a fractional configuration value in the inclusive range zero to one.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The configured fraction or <paramref name="fallback" />.</returns>
    private static double ReadFraction(IConfiguration configuration, string key, double fallback) =>
        double.TryParse(configuration[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 && value <= 1
            ? value
            : fallback;

    /// <summary>Reads one positive duration configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The positive configured duration or <paramref name="fallback" />.</returns>
    private static TimeSpan ReadPositiveTimeSpan(IConfiguration configuration, string key, TimeSpan fallback) =>
        TimeSpan.TryParse(configuration[key], out var value) && value > TimeSpan.Zero ? value : fallback;

    /// <summary>Reads one Boolean configuration value.</summary>
    /// <param name="configuration">The engine configuration. Cannot be <see langword="null" />.</param>
    /// <param name="key">The configuration key. Cannot be <see langword="null" />.</param>
    /// <param name="fallback">The value used when the key is absent or invalid.</param>
    /// <returns>The configured value or <paramref name="fallback" />.</returns>
    private static bool ReadBoolean(IConfiguration configuration, string key, bool fallback) =>
        bool.TryParse(configuration[key], out var value) ? value : fallback;

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
        builder.Services.AddSingleton(CreateFrozenGitTreeReaderOptions(builder.Configuration));
        builder.Services.AddSingleton(CreateChangeAnatomyOptions(builder.Configuration));
        builder.Services.AddScoped<IFrozenGitTreeReaderFactory, FrozenGitTreeReaderFactory>();
        builder.Services.AddScoped<IChangeAnatomyService, ChangeAnatomyService>();
        builder.Services.AddSingleton(CreateCorrespondenceOptions(builder.Configuration));
        builder.Services.AddScoped<ICorrespondenceRankingService, CorrespondenceRankingService>();
        builder.Services.AddSingleton(CreateEvidenceGraphOptions(builder.Configuration));
        builder.Services.AddScoped<IEvidenceGraphService, EvidenceGraphService>();
        builder.Services.AddSingleton(CreateContextPolicyOptions(builder.Configuration));
        builder.Services.AddScoped<IContextPolicyService, ContextPolicyService>();
        var binderOptions = CreateEvidenceBinderOptions(builder.Configuration);
        var curatorOptions = CreateCuratorOptions(builder.Configuration);
        CuratorConfigurationValidator.Validate(binderOptions, curatorOptions);
        builder.Services.AddSingleton(binderOptions);
        builder.Services.AddSingleton(curatorOptions);
        builder.Services.AddScoped<IEvidenceBinderService, EvidenceBinderService>();
        builder.Services.Configure<ModelCompletionOptions>(options =>
        {
            var configured = CreateModelCompletionOptions(builder.Configuration);
            options.BaseUrl = configured.BaseUrl;
            options.Model = configured.Model;
            options.ApiKey = configured.ApiKey;
            options.RequestTimeout = configured.RequestTimeout;
        });
        builder.Services.AddModelCompletionClient();
        builder.Services.AddScoped<ICuratorService, CuratorService>();
        builder.Services.AddScoped<IDraftValidationService, DraftValidationService>();
        builder.Services.AddScoped<ISnapshotCaptureService, GitSnapshotCaptureService>();
        builder.Services.AddScoped<IAnalysisPipeline, ShallowAnalysisPipeline>();
        builder.Services.AddScoped<IAnalysisRunCoordinator, AnalysisRunCoordinator>();
        builder.Services.AddScoped<IRepositoryBusyGuard, RepositoryBusyGuard>();
        builder.Services.AddHostedService<AnalysisProcessorHost>();
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
