using ChangeLens.Core.Analysis.ChangeAnatomy.Constants;
using ChangeLens.Core.Analysis.ChangeAnatomy.Interfaces;
using ChangeLens.Core.Analysis.ChangeAnatomy.Models;
using ChangeLens.Core.Analysis.ChangeAnatomy.Services;
using ChangeLens.Core.Analysis.ClaimChecking.Constants;
using ChangeLens.Core.Analysis.ClaimChecking.Interfaces;
using ChangeLens.Core.Analysis.ClaimChecking.Models;
using ChangeLens.Core.Analysis.ClaimChecking.Services;
using ChangeLens.Core.Analysis.ContextPolicy.Constants;
using ChangeLens.Core.Analysis.ContextPolicy.Interfaces;
using ChangeLens.Core.Analysis.ContextPolicy.Models;
using ChangeLens.Core.Analysis.ContextPolicy.Services;
using ChangeLens.Core.Analysis.Correspondence.Constants;
using ChangeLens.Core.Analysis.Correspondence.Interfaces;
using ChangeLens.Core.Analysis.Correspondence.Models;
using ChangeLens.Core.Analysis.Correspondence.Services;
using ChangeLens.Core.Analysis.Curation.Constants;
using ChangeLens.Core.Analysis.Curation.Interfaces;
using ChangeLens.Core.Analysis.Curation.Models;
using ChangeLens.Core.Analysis.Curation.Services;
using ChangeLens.Core.Analysis.DraftValidation.Interfaces;
using ChangeLens.Core.Analysis.DraftValidation.Services;
using ChangeLens.Core.Analysis.EvidenceBinder.Constants;
using ChangeLens.Core.Analysis.EvidenceBinder.Interfaces;
using ChangeLens.Core.Analysis.EvidenceBinder.Models;
using ChangeLens.Core.Analysis.EvidenceBinder.Services;
using ChangeLens.Core.Analysis.EvidenceFrontier.Constants;
using ChangeLens.Core.Analysis.EvidenceFrontier.Interfaces;
using ChangeLens.Core.Analysis.EvidenceFrontier.Models;
using ChangeLens.Core.Analysis.EvidenceFrontier.Services;
using ChangeLens.Core.Analysis.EvidenceGraph.Constants;
using ChangeLens.Core.Analysis.EvidenceGraph.Interfaces;
using ChangeLens.Core.Analysis.EvidenceGraph.Models;
using ChangeLens.Core.Analysis.EvidenceGraph.Services;
using ChangeLens.Core.Analysis.FindingValidation.Interfaces;
using ChangeLens.Core.Analysis.FindingValidation.Services;
using ChangeLens.Core.Analysis.Publication.Interfaces;
using ChangeLens.Core.Analysis.Publication.Services;
using ChangeLens.Core.Analysis.Review.Constants;
using ChangeLens.Core.Analysis.Review.Interfaces;
using ChangeLens.Core.Analysis.Review.Models;
using ChangeLens.Core.Analysis.Review.Services;
using ChangeLens.Core.Analysis.Snapshots.Constants;
using ChangeLens.Core.Analysis.Snapshots.Interfaces;
using ChangeLens.Core.Analysis.Snapshots.Models;
using ChangeLens.Core.Analysis.Snapshots.Services;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Engine.Analysis.Helpers;
using ChangeLens.Engine.Analysis.Pipeline.Interfaces;
using ChangeLens.Engine.Analysis.Pipeline.Services;
using ChangeLens.Infrastructure.ModelCompletion.Constants;
using ChangeLens.Infrastructure.ModelCompletion.Extensions;
using ChangeLens.Infrastructure.ModelCompletion.Helpers;
using ChangeLens.Infrastructure.ModelCompletion.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Analysis.Extensions;

/// <summary>
///     Registers analysis pipeline stages, options, and model-completion infrastructure on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class AnalysisHostApplicationBuilderExtensions
{
    /// <summary>Registers analysis pipeline options, stage services, model-completion infrastructure, and pipeline execution.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddAnalysisServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

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
        builder.Services.AddSingleton(CreateEvidenceFrontierOptions(builder.Configuration));
        builder.Services.AddSingleton(CreateClaimCheckingOptions(builder.Configuration));
        builder.Services.AddScoped<IEvidenceFrontierService, EvidenceFrontierService>();
        builder.Services.AddScoped<IClaimCheckingService, ClaimCheckingService>();
        builder.Services.AddScoped<IPublicationService, PublicationService>();
        var binderOptions = CreateEvidenceBinderOptions(builder.Configuration);
        var curatorOptions = CreateCuratorOptions(builder.Configuration);
        var reviewerOptions = CreateReviewerOptions(builder.Configuration);
        builder.Services.AddSingleton(binderOptions);
        builder.Services.AddSingleton(curatorOptions);
        builder.Services.AddSingleton(reviewerOptions);
        builder.Services.AddScoped<IEvidenceBinderService, EvidenceBinderService>();
        builder.Services.Configure<ModelCompletionOptions>(options =>
        {
            var configured = CreateModelCompletionOptions(builder.Configuration);
            options.BaseUrl = configured.BaseUrl;
            options.Model = configured.Model;
            options.ApiKey = configured.ApiKey;
            options.RequestTimeout = configured.RequestTimeout;
            options.OpenRouterSort = configured.OpenRouterSort;
            options.OpenRouterIgnore = configured.OpenRouterIgnore;
            options.MaximumResponseBytes = ModelCompletionTransportConstants.ResponseByteBudget(
                Math.Max(curatorOptions.MaximumOutputCharacters, reviewerOptions.MaximumOutputCharacters));
        });
        builder.Services.AddModelCompletionInfrastructure();
        builder.Services.AddScoped<ICuratorService, CuratorService>();
        builder.Services.AddScoped<IReviewerService, ReviewerService>();
        builder.Services.AddScoped<IFindingValidationService, FindingValidationService>();
        builder.Services.AddScoped<IClaimChecker, ModelClaimChecker>();
        builder.Services.AddScoped<IDraftValidationService, DraftValidationService>();
        builder.Services.AddScoped<ISnapshotCaptureService, GitSnapshotCaptureService>();
        builder.Services.AddScoped<IAnalysisCaptureStep, AnalysisCaptureStep>();
        builder.Services.AddScoped<IAnalysisDiscoverStep, AnalysisDiscoverStep>();
        builder.Services.AddScoped<IAnalysisCurationCoordinator, AnalysisCurationCoordinator>();
        builder.Services.AddScoped<IAnalysisPublicationStep, AnalysisPublicationStep>();
        builder.Services.AddScoped<IAnalysisPipeline, AnalysisPipeline>();
    }

    private static FrozenGitTreeReaderOptions CreateFrozenGitTreeReaderOptions(IConfiguration configuration)
    {
        var defaults = new FrozenGitTreeReaderOptions();
        return new FrozenGitTreeReaderOptions
        {
            MaximumBlobBytes = ConfigurationValueReader.ReadPositiveInt(
                configuration, FrozenGitTreeReaderConfigurationConstants.MaximumBlobBytesKey, defaults.MaximumBlobBytes),
            MaximumTreeFiles = ConfigurationValueReader.ReadPositiveInt(
                configuration, FrozenGitTreeReaderConfigurationConstants.MaximumTreeFilesKey, defaults.MaximumTreeFiles),
            MaximumHistoryCommits = ConfigurationValueReader.ReadPositiveInt(
                configuration, FrozenGitTreeReaderConfigurationConstants.MaximumHistoryCommitsKey, defaults.MaximumHistoryCommits),
            MaximumHistoryPathsPerCommit = ConfigurationValueReader.ReadPositiveInt(
                configuration, FrozenGitTreeReaderConfigurationConstants.MaximumHistoryPathsPerCommitKey,
                defaults.MaximumHistoryPathsPerCommit),
            CommandTimeout = ConfigurationValueReader.ReadPositiveTimeSpan(
                configuration, FrozenGitTreeReaderConfigurationConstants.CommandTimeoutKey, defaults.CommandTimeout),
        };
    }

    private static ChangeAnatomyOptions CreateChangeAnatomyOptions(IConfiguration configuration)
    {
        var defaults = new ChangeAnatomyOptions();
        return new ChangeAnatomyOptions
        {
            MinimumKeyLength = ConfigurationValueReader.ReadPositiveInt(
                configuration, ChangeAnatomyConfigurationConstants.MinimumKeyLengthKey, defaults.MinimumKeyLength),
            MaximumKeysPerFile = ConfigurationValueReader.ReadPositiveInt(
                configuration, ChangeAnatomyConfigurationConstants.MaximumKeysPerFileKey, defaults.MaximumKeysPerFile),
            MaximumOccurrencesPerKey = ConfigurationValueReader.ReadPositiveInt(
                configuration, ChangeAnatomyConfigurationConstants.MaximumOccurrencesPerKeyKey, defaults.MaximumOccurrencesPerKey),
        };
    }

    private static CorrespondenceOptions CreateCorrespondenceOptions(IConfiguration configuration)
    {
        var defaults = new CorrespondenceOptions();
        return new CorrespondenceOptions
        {
            MaximumCandidates = ConfigurationValueReader.ReadPositiveInt(
                configuration, CorrespondenceConfigurationConstants.MaximumCandidatesKey, defaults.MaximumCandidates),
            MaximumIndexedKeysPerFile = ConfigurationValueReader.ReadPositiveInt(
                configuration, CorrespondenceConfigurationConstants.MaximumIndexedKeysPerFileKey, defaults.MaximumIndexedKeysPerFile),
            MaximumReasonsPerCandidate = ConfigurationValueReader.ReadPositiveInt(
                configuration, CorrespondenceConfigurationConstants.MaximumReasonsPerCandidateKey, defaults.MaximumReasonsPerCandidate),
            IncludeCoChange = ConfigurationValueReader.ReadBoolean(
                configuration, CorrespondenceConfigurationConstants.IncludeCoChangeKey, defaults.IncludeCoChange),
        };
    }

    private static EvidenceGraphOptions CreateEvidenceGraphOptions(IConfiguration configuration)
    {
        var defaults = new EvidenceGraphOptions();
        return new EvidenceGraphOptions
        {
            MaximumQuoteWindowNodes = ConfigurationValueReader.ReadPositiveInt(
                configuration, EvidenceGraphConfigurationConstants.MaximumQuoteWindowNodesKey, defaults.MaximumQuoteWindowNodes),
            MaximumEdges = ConfigurationValueReader.ReadPositiveInt(
                configuration, EvidenceGraphConfigurationConstants.MaximumEdgesKey, defaults.MaximumEdges),
            MaximumNodesPerChangedFile = ConfigurationValueReader.ReadPositiveInt(
                configuration, EvidenceGraphConfigurationConstants.MaximumNodesPerChangedFileKey, defaults.MaximumNodesPerChangedFile),
            MaximumWholeChangedFileLines = ConfigurationValueReader.ReadNonNegativeInt(
                configuration, EvidenceGraphConfigurationConstants.MaximumWholeChangedFileLinesKey, defaults.MaximumWholeChangedFileLines),
            MaximumBlockExtensionLines = ConfigurationValueReader.ReadNonNegativeInt(
                configuration, EvidenceGraphConfigurationConstants.MaximumBlockExtensionLinesKey, defaults.MaximumBlockExtensionLines),
        };
    }

    private static ContextPolicyOptions CreateContextPolicyOptions(IConfiguration configuration)
    {
        var defaults = new ContextPolicyOptions();
        return new ContextPolicyOptions
        {
            MaximumDisclosedCharactersPerNode = ConfigurationValueReader.ReadPositiveInt(
                configuration, ContextPolicyConfigurationConstants.MaximumDisclosedCharactersPerNodeKey,
                defaults.MaximumDisclosedCharactersPerNode),
        };
    }

    private static EvidenceFrontierOptions CreateEvidenceFrontierOptions(IConfiguration configuration)
    {
        var defaults = new EvidenceFrontierOptions();
        return new EvidenceFrontierOptions
        {
            MaximumEntries = ConfigurationValueReader.ReadInt(
                configuration, EvidenceFrontierConfigurationConstants.MaximumEntriesKey, defaults.MaximumEntries),
        };
    }

    private static ClaimCheckingOptions CreateClaimCheckingOptions(IConfiguration configuration)
    {
        var defaults = new ClaimCheckingOptions();
        return new ClaimCheckingOptions
        {
            Enabled = ConfigurationValueReader.ReadBoolean(configuration, ClaimCheckingConfigurationConstants.EnabledKey, defaults.Enabled),
            MaximumClaims = ConfigurationValueReader.ReadInt(
                configuration, ClaimCheckingConfigurationConstants.MaximumClaimsKey, defaults.MaximumClaims),
            MaximumPayloadCharacters = ConfigurationValueReader.ReadInt(
                configuration, ClaimCheckingConfigurationConstants.MaximumPayloadCharactersKey, defaults.MaximumPayloadCharacters),
            Attempts = ConfigurationValueReader.ReadInt(
                configuration, ClaimCheckingConfigurationConstants.AttemptsKey, defaults.Attempts),
            ReasoningEffort = ConfigurationValueReader.ReadEnum(
                configuration, ClaimCheckingConfigurationConstants.ReasoningEffortKey, defaults.ReasoningEffort),
            MaximumOutputTokens = ConfigurationValueReader.ReadInt(
                configuration, ClaimCheckingConfigurationConstants.MaximumOutputTokensKey, defaults.MaximumOutputTokens),
        };
    }

    private static EvidenceBinderOptions CreateEvidenceBinderOptions(IConfiguration configuration)
    {
        var defaults = new EvidenceBinderOptions();
        return new EvidenceBinderOptions
        {
            ContextWindowTokens = ConfigurationValueReader.ReadPositiveInt(
                configuration, EvidenceBinderConfigurationConstants.ContextWindowTokensKey, defaults.ContextWindowTokens),
            CuratorOutputCharacters = ConfigurationValueReader.ReadNonNegativeInt(
                configuration, EvidenceBinderConfigurationConstants.CuratorOutputCharactersKey, defaults.CuratorOutputCharacters),
            PromptReserveCharacters = ConfigurationValueReader.ReadNonNegativeInt(
                configuration, EvidenceBinderConfigurationConstants.PromptReserveCharactersKey, defaults.PromptReserveCharacters),
            MaximumBinderCharacters = ConfigurationValueReader.ReadOptionalPositiveInt(
                configuration, EvidenceBinderConfigurationConstants.MaximumBinderCharactersKey),
            TargetUtilization = ConfigurationValueReader.ReadFraction(
                configuration, EvidenceBinderConfigurationConstants.TargetUtilizationKey, defaults.TargetUtilization),
        };
    }

    private static CuratorOptions CreateCuratorOptions(IConfiguration configuration)
    {
        var defaults = new CuratorOptions();
        return new CuratorOptions
        {
            MaximumOutputCharacters = ConfigurationValueReader.ReadPositiveInt(
                configuration, CuratorConfigurationConstants.MaximumOutputCharactersKey, defaults.MaximumOutputCharacters),
            MaximumOutputTokens = ConfigurationValueReader.ReadPositiveInt(
                configuration, CuratorConfigurationConstants.MaximumOutputTokensKey, defaults.MaximumOutputTokens),
            ReasoningEffort = ConfigurationValueReader.ReadEnum(
                configuration, CuratorConfigurationConstants.ReasoningEffortKey, defaults.ReasoningEffort),
        };
    }

    private static ReviewerOptions CreateReviewerOptions(IConfiguration configuration)
    {
        var defaults = new ReviewerOptions();
        return new ReviewerOptions
        {
            Enabled = ConfigurationValueReader.ReadBoolean(configuration, ReviewerConfigurationConstants.EnabledKey, defaults.Enabled),
            MaximumOutputCharacters = ConfigurationValueReader.ReadPositiveInt(
                configuration, ReviewerConfigurationConstants.MaximumOutputCharactersKey, defaults.MaximumOutputCharacters),
            MaximumOutputTokens = ConfigurationValueReader.ReadPositiveInt(
                configuration, ReviewerConfigurationConstants.MaximumOutputTokensKey, defaults.MaximumOutputTokens),
            ReasoningEffort = ConfigurationValueReader.ReadEnum(
                configuration, ReviewerConfigurationConstants.ReasoningEffortKey, defaults.ReasoningEffort),
            Model = string.IsNullOrWhiteSpace(configuration[ReviewerConfigurationConstants.ModelKey])
                ? defaults.Model
                : configuration[ReviewerConfigurationConstants.ModelKey]!.Trim(),
        };
    }

    private static ModelCompletionOptions CreateModelCompletionOptions(IConfiguration configuration)
    {
        var defaults = new ModelCompletionOptions();
        return new ModelCompletionOptions
        {
            BaseUrl = configuration[ModelCompletionConfigurationConstants.BaseUrlKey],
            Model = configuration[ModelCompletionConfigurationConstants.ModelKey],
            ApiKey = ModelCompletionApiKeyFileReader.Read(configuration[ModelCompletionConfigurationConstants.ApiKeyFileKey]),
            RequestTimeout = ConfigurationValueReader.ReadPositiveTimeSpan(
                configuration, ModelCompletionConfigurationConstants.RequestTimeoutKey, defaults.RequestTimeout),
            OpenRouterSort = configuration[ModelCompletionConfigurationConstants.OpenRouterSortKey],
            OpenRouterIgnore = configuration[ModelCompletionConfigurationConstants.OpenRouterIgnoreKey]?.Split(',') ?? [],
        };
    }
}
