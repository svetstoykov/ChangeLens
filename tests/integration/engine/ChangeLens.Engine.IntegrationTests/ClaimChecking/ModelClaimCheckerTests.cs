using System.Text.Json;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ClaimChecking.Constants;
using ChangeLens.Core.ClaimChecking.Interfaces;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.ClaimChecking.Services;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceFrontier.Models;
using ChangeLens.Core.EvidenceFrontier.Services;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.ModelCompletion.Constants;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Publication.Interfaces;
using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Publication.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Hosting.Extensions;
using ChangeLens.Engine.Hosting.Helpers;
using ChangeLens.Engine.IntegrationTests.Analysis.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.IntegrationTests.Publication.Support;
using ChangeLens.Engine.IntegrationTests.Support;
using ChangeLens.Engine.Logging.Constants;
using ChangeLens.Engine.Logging.Extensions;
using ChangeLens.Infrastructure.LocalState.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.ClaimChecking;

/// <summary>
///     Verifies the model-backed checker's prompt, payload fitting, reply parsing, retries, and publication outcome.
/// </summary>
public sealed class ModelClaimCheckerTests
{
    private const string LongExplanation = "Explanation words that only this relationship claim carries and that must travel whole";

    [Fact]
    public async Task EnabledCheckerStartsWithTheModelAdapter()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration[LocalStateConstants.DirectoryConfigurationKey] = temporaryDirectory.DirectoryPath;
        builder.Configuration[EngineLoggingConstants.FileDirectoryConfigurationKey] = Path.Combine(temporaryDirectory.DirectoryPath, "logs");
        builder.Configuration[ClaimCheckingConfigurationConstants.EnabledKey] = "true";
        builder.ConfigureContainer(
            new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }),
            static _ => { });
        builder.AddEngineLogging();
        builder.AddRuntimeServices();
        builder.AddLocalStateServices();
        builder.AddPreferenceServices();
        builder.AddEngineStatusServices();
        builder.AddRepositoryServices();
        builder.AddComparisonServices();
        builder.AddAnalysisRunServices();
        builder.AddProtocolServices();
        builder.AddActionHandlers();

        EngineStartupValidator.Validate(builder.Services);
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();

        Assert.True(scope.ServiceProvider.GetRequiredService<ClaimCheckingOptions>().Enabled);
        Assert.IsType<ModelClaimChecker>(scope.ServiceProvider.GetRequiredService<IClaimChecker>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPublicationService>());
    }

    [Fact]
    public void EnabledCheckerWithNonPositiveLimitIsRejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ClaimCheckingOptions { Enabled = true, Attempts = 0 });
        services.AddScoped<IClaimChecker, ModelClaimChecker>();

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(services));

        Assert.Contains("must all be positive", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckedPublicationMarksClaimsCheckedAndThesisDerived()
    {
        var client = new ScriptedModelCompletionClient(request => Completion(SupportedReply(request)));
        var binder = PublicationTestFixtures.Binder("n1", "n2");
        var validation = Validation(Draft(steps: [new BoundStatement("Step", ["n2"])]));

        var result = await Publication(client).PublishAsync(Request(validation, binder), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, client.CallCount);
        var citations = result.Data!.ReadingModel.Citations;
        Assert.Contains(citations, citation => citation.ClaimId == "thesis");
        Assert.All(citations.Where(citation => citation.ClaimId == "thesis"),
            citation => Assert.Equal(CitationProvenance.Derived, citation.Provenance));
        var judged = citations.Where(citation => citation.ClaimId is "track:t1:summary" or "track:t1:step:0").ToArray();
        Assert.Equal(2, judged.Length);
        Assert.All(judged, citation => Assert.Equal(CitationProvenance.Checked, citation.Provenance));
        Assert.DoesNotContain(result.Data.ReadingModel.Assurances, assurance => assurance.Kind == ReadingAssuranceKind.CheckerFailed);
    }

    [Fact]
    public async Task UnreadableReplyTwiceYieldsCheckerFailedPublicationWithNoMentalModel()
    {
        var client = new ScriptedModelCompletionClient(_ => Completion("I checked the claims and they look right."));
        var binder = PublicationTestFixtures.Binder("n1");
        var validation = Validation(Draft());

        var result = await Publication(client).PublishAsync(Request(validation, binder), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, client.CallCount);
        Assert.Contains("RETRY — YOUR PREVIOUS REPLY WAS DISCARDED", client.LastRequest!.SystemMessage, StringComparison.Ordinal);
        Assert.Null(result.Data!.ReadingModel.Thesis);
        Assert.Empty(result.Data.ReadingModel.Areas);
        Assert.Contains(result.Data.ReadingModel.Assurances, assurance => assurance.Kind == ReadingAssuranceKind.CheckerFailed);
    }

    [Fact]
    public async Task RetryResendsTheSamePayloadAndAcceptsAReadableSecondReply()
    {
        var requests = new List<ModelCompletionRequest>();
        var client = new ScriptedModelCompletionClient(request =>
        {
            requests.Add(request);
            return Completion(requests.Count == 1 ? """{"checks":[]}""" : SupportedReply(request));
        });
        var binder = PublicationTestFixtures.Binder("n1");
        var validation = Validation(Draft(steps: [new BoundStatement("Step", ["n1"])]));

        var result = await Checking(client).CheckAsync(validation, binder, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Summary.ParseFailure);
        Assert.Equal(2, requests.Count);
        Assert.Equal(requests[0].UserMessage, requests[1].UserMessage);
        Assert.DoesNotContain("RETRY", requests[0].SystemMessage, StringComparison.Ordinal);
        Assert.StartsWith(requests[0].SystemMessage, requests[1].SystemMessage, StringComparison.Ordinal);
        Assert.Single(result.Data.Model.Tracks);
    }

    [Fact]
    public async Task ProviderFailureIsForwardedWithoutRetry()
    {
        var error = OperationError.ExternalDependencyFailure("provider unavailable", ModelCompletionErrorCode.ProviderUnavailable);
        var client = new ScriptedModelCompletionClient(_ => Result.Fail<ModelCompletionModel>(error));

        var result = await Checker(client).CheckAsync([StatementClaim("track:t1:summary", "n1")], TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Same(error, Assert.Single(result.Errors));
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task ClaimsPastThePayloadBoundAreReportedUncheckedNotRemoved()
    {
        var steps = new[] { new BoundStatement("Step one", ["n1"]), new BoundStatement("Step two", ["n1"]) };
        var binder = PublicationTestFixtures.Binder("n1");
        var fullPayload = await CapturePayloadAsync(Validation(Draft(steps: steps)), binder);
        var summaryClaim = JsonDocument.Parse(fullPayload).RootElement.GetProperty("claims")[0].GetRawText();
        var options = new ClaimCheckingOptions { MaximumPayloadCharacters = CheckerJson.EmptyPayload.Length + summaryClaim.Length };
        var client = new ScriptedModelCompletionClient(request => Completion(SupportedReply(request)));

        var result = await Checking(client, options).CheckAsync(Validation(Draft(steps: steps)), binder, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["track:t1:summary"], SubmittedClaimIds(client.LastRequest!));
        Assert.Equal(["track:t1:step:0", "track:t1:step:1"], result.Data!.Summary.UncheckedClaimIds);
        Assert.Empty(result.Data.Summary.RemovedClaimIds);
        Assert.Equal(0, result.Data.Summary.RemovedCount);
    }

    [Fact]
    public async Task PayloadCarriesTheCompleteExplanationAndAnOversizedRelationshipIsOmittedWhole()
    {
        var binder = PublicationTestFixtures.Binder("n1", "n2");
        var validation = Validation(Draft(relationships: [new DraftRelationship(
            "r1", "p1", "p2", CuratorContractConstants.Invokes, LongExplanation, ["n1", "n2"], [])]));
        var fullPayload = await CapturePayloadAsync(validation, binder);
        var complete = new ScriptedModelCompletionClient(request => Completion(SupportedReply(request)));

        await Checking(complete).CheckAsync(validation, binder, TestContext.Current.CancellationToken);

        Assert.Contains("Explanation: " + LongExplanation, complete.LastRequest!.UserMessage, StringComparison.Ordinal);
        Assert.Contains(
            "A relationship whose direction and kind are shown but whose explanation asserts\n",
            complete.LastRequest.SystemMessage.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("Unsupported — supported kind, unsupported explanation", complete.LastRequest.SystemMessage, StringComparison.Ordinal);

        var bounded = new ScriptedModelCompletionClient(request => Completion(SupportedReply(request)));
        var options = new ClaimCheckingOptions { MaximumPayloadCharacters = fullPayload.Length - 1 };
        var result = await Checking(bounded, options).CheckAsync(validation, binder, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("track:t1:relationship:r1", SubmittedClaimIds(bounded.LastRequest!));
        Assert.DoesNotContain("only this relationship claim", bounded.LastRequest!.UserMessage, StringComparison.Ordinal);
        Assert.Equal(["track:t1:relationship:r1"], result.Data!.Summary.UncheckedClaimIds);
    }

    [Fact]
    public async Task TwoDisjointFocusRangesForOneNodeReachCoreIntact()
    {
        var claim = StatementClaim("track:t1:summary", "n1", 10, 42);
        var reply = """
            {"checks":[{"claimId":"track:t1:summary","verdict":"Supported","correctedKind":null,
            "focus":[{"nodeId":"n1","startLine":12,"endLine":13},{"nodeId":"n1","startLine":30,"endLine":31}]}]}
            """;
        var client = new ScriptedModelCompletionClient(_ => Completion(reply));

        var result = await Checker(client).CheckAsync([claim], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var verdict = Assert.Single(result.Data!.Verdicts);
        Assert.Equal([new FocusRange("n1", 12, 13), new FocusRange("n1", 30, 31)], verdict.Focus);
    }

    [Fact]
    public async Task UnreadableVerdictEntriesAreSkippedRatherThanTreatedAsSupport()
    {
        var reply = """
            {"checks":[{"claimId":"a","verdict":"0","correctedKind":null,"focus":null},
            {"claimId":"b","correctedKind":null,"focus":null},
            {"claimId":"c","verdict":"Unsupported","correctedKind":null,"focus":null}]}
            """;
        var client = new ScriptedModelCompletionClient(_ => Completion(reply));

        var result = await Checker(client).CheckAsync(
            [StatementClaim("a", "n1"), StatementClaim("b", "n1"), StatementClaim("c", "n1")], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var verdict = Assert.Single(result.Data!.Verdicts);
        Assert.Equal("c", verdict.ClaimId);
        Assert.Equal(ClaimVerdictKind.Unsupported, verdict.Kind);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task ReasoningAndContentDivergenceIsLoggedAsAWarning()
    {
        var logger = new RecordingLogger<ModelClaimChecker>();
        var client = new ScriptedModelCompletionClient(request => Completion(SupportedReply(request), outputTokens: 5_000));
        var checker = new ModelClaimChecker(client, new ClaimCheckingOptions(), logger);

        var result = await checker.CheckAsync([StatementClaim("track:t1:summary", "n1")], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Contains(LogLevel.Warning, logger.Levels);
    }

    private static async Task<string> CapturePayloadAsync(DraftValidationOutcome validation, EvidenceBinderModel binder)
    {
        var client = new ScriptedModelCompletionClient(request => Completion(SupportedReply(request)));
        await Checking(client).CheckAsync(validation, binder, TestContext.Current.CancellationToken);
        return client.LastRequest!.UserMessage;
    }

    private static ModelClaimChecker Checker(ScriptedModelCompletionClient client, ClaimCheckingOptions? options = null) =>
        new(client, options ?? new ClaimCheckingOptions(), NullLogger<ModelClaimChecker>.Instance);

    private static ClaimCheckingService Checking(ScriptedModelCompletionClient client, ClaimCheckingOptions? options = null) =>
        new(Checker(client, options));

    private static PublicationService Publication(ScriptedModelCompletionClient client)
    {
        var options = new ClaimCheckingOptions { Enabled = true };
        var checker = Checker(client, options);
        return new PublicationService(new ClaimCheckingService(checker), new EvidenceFrontierService(new EvidenceFrontierOptions()), options,
            NullLogger<PublicationService>.Instance, checker);
    }

    private static PublicationRequest Request(DraftValidationOutcome validation, EvidenceBinderModel binder)
    {
        var nodeIds = binder.Evidence.Select(evidence => evidence.NodeId).ToArray();
        return new PublicationRequest(validation, binder, PublicationTestFixtures.Graph(nodeIds), PublicationTestFixtures.Policy(),
            PublicationTestFixtures.Ranking());
    }

    private static DraftValidationOutcome Validation(MentalModelDraft draft) => new(draft, [], 0, 0, 0, 0);

    private static MentalModelDraft Draft(
        IReadOnlyList<DraftRelationship>? relationships = null,
        IReadOnlyList<BoundStatement>? steps = null) => new(
        new BoundStatement("Thesis", ["n1"]),
        [new DraftTrack(
            "t1",
            "Track",
            new BoundStatement("Summary", ["n1"]),
            relationships is null ? CuratorContractConstants.Walk : CuratorContractConstants.ParticipantMap,
            [new DraftParticipant("p1", "CreateOrder", "handler", true, ["n1"]), new DraftParticipant("p2", "Save", "persistence", true, ["n2"])],
            relationships ?? [],
            steps ?? [],
            [])],
        []);

    private static CheckerClaim StatementClaim(string claimId, string nodeId, int startLine = 1, int endLine = 1) => new(
        claimId,
        CheckerClaimType.Summary,
        "Summary",
        null,
        null,
        null,
        [new CheckerQuote(nodeId, $"src/{nodeId}.cs", ChangeAnatomySide.After, startLine, endLine, null, $"{startLine}| quote")]);

    private static IReadOnlyList<string> SubmittedClaimIds(ModelCompletionRequest request) =>
        JsonDocument.Parse(request.UserMessage).RootElement.GetProperty("claims").EnumerateArray()
            .Select(claim => claim.GetProperty("claimId").GetString()!)
            .ToArray();

    private static string SupportedReply(ModelCompletionRequest request) => JsonSerializer.Serialize(new
    {
        checks = SubmittedClaimIds(request)
            .Select(claimId => new { claimId, verdict = "Supported", correctedKind = (string?)null, focus = (int[]?)null }),
    });

    private static Result<ModelCompletionModel> Completion(string text, int? outputTokens = null) =>
        Result.Success(new ModelCompletionModel("fixture-model", text, 5, 100, outputTokens, null, null));
}
