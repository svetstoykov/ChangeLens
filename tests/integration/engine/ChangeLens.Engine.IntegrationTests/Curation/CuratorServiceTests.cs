using System.Text;
using ChangeLens.Core.Curation.Constants;
using ChangeLens.Core.Curation.Interfaces;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.Curation.Services;
using ChangeLens.Core.DraftValidation.Services;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceBinder.Services;
using ChangeLens.Core.ModelCompletion.Constants;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Hosting.Extensions;
using ChangeLens.Engine.Hosting.Helpers;
using ChangeLens.Engine.IntegrationTests.Curation.Support;
using ChangeLens.Engine.IntegrationTests.DraftValidation.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Curation;

/// <summary>
///     Verifies one-call curation, strict draft parsing, prompt budgeting, and the curator-validator boundary.
/// </summary>
public sealed class CuratorServiceTests
{
    [Fact]
    public async Task GoodReplyParsesIntoADraftAndUsesTheBinderPayloadVerbatim()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2", "n3"]);
        var fake = new FakeModelCompletionClient(Result.Success(Completion(ValidJson())));
        var service = Service(fake);

        var result = await service.CurateAsync(binder, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.Diagnostics.ParseFailureReason);
        Assert.Equal("t1", Assert.Single(result.Data.Draft.Tracks).Id);
        Assert.Equal(1, fake.CallCount);
        Assert.NotNull(fake.LastRequest);
        Assert.Equal(EvidenceBinderJson.SerializePayload(binder), fake.LastRequest!.UserMessage);
        Assert.DoesNotContain("matchEdges", fake.LastRequest.UserMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    public async Task UnparseableRepliesBecomeSuccessfulRecordedParseFailures(string reply)
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1"]);
        var service = Service(new FakeModelCompletionClient(Result.Success(Completion(reply))));

        var result = await service.CurateAsync(binder, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Draft.Tracks);
        Assert.NotNull(result.Data.Diagnostics.ParseFailureReason);
        Assert.Equal(reply.Length, result.Data.Diagnostics.OutputCharacters);
    }

    [Fact]
    public async Task OverCapReplyBecomesASuccessfulRecordedParseFailure()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1"]);
        var options = new CuratorOptions { MaximumOutputCharacters = 3, MaximumOutputTokens = 1 };
        var service = new CuratorService(
            new FakeModelCompletionClient(Result.Success(Completion("{}{}"))),
            options,
            NullLogger<CuratorService>.Instance);

        var result = await service.CurateAsync(binder, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Draft.Tracks);
        Assert.Contains("exceeds", result.Data.Diagnostics.ParseFailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderFailureForwardsThePortErrorUnchanged()
    {
        var providerError = OperationError.ExternalDependencyFailure("rate limited", ModelCompletionErrorCode.RateLimited);
        var fake = new FakeModelCompletionClient(Result.Fail<ModelCompletionModel>(providerError));
        var service = Service(fake);

        var result = await service.CurateAsync(DraftValidationFixtureBinderBuilder.Create(["n1"]), CancellationToken.None);

        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Same(providerError, error);
        Assert.Equal(ModelCompletionErrorCode.RateLimited, error.Code);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData("{\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[{\"id\":\"t1\",\"title\":\"Track\",\"summary\":{\"text\":\"summary\",\"evidenceNodeIds\":[\"n1\"]},\"shape\":\"ParticipantMap\",\"participants\":[{\"id\":\"p1\",\"name\":\"Name\",\"changed\":\"true\",\"role\":\"Role\",\"evidenceNodeIds\":[\"n1\"]}],\"relationships\":[],\"orderedSteps\":[],\"purposes\":[]}],\"droppedNodeIds\":[]}")]
    [InlineData("{\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"focus\":\"claim\",\"tracks\":[],\"droppedNodeIds\":[]}")]
    [InlineData("{\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[null],\"droppedNodeIds\":[]}")]
    public async Task InvalidRequiredShapeUnknownPropertiesAndNullElementsFailTheWholeReply(string reply)
    {
        var service = Service(new FakeModelCompletionClient(Result.Success(Completion(reply))));

        var result = await service.CurateAsync(DraftValidationFixtureBinderBuilder.Create(["n1"]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Draft.Tracks);
        Assert.NotNull(result.Data.Diagnostics.ParseFailureReason);
    }

    [Fact]
    public async Task MissingAndNullArraysNormalizeToEmptyLists()
    {
        const string reply = "{\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":null},\"droppedNodeIds\":null}";
        var service = Service(new FakeModelCompletionClient(Result.Success(Completion(reply))));

        var result = await service.CurateAsync(DraftValidationFixtureBinderBuilder.Create(["n1"]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var draft = result.Data!.Draft;
        Assert.Empty(draft.Tracks);
        Assert.Empty(draft.DroppedNodeIds);
        Assert.Empty(draft.Thesis.EvidenceNodeIds);
        Assert.Null(result.Data.Diagnostics.ParseFailureReason);
    }

    [Fact]
    public void RenderedPromptContainsTheSharedContractAndBothWorkedExamples()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1"]);

        var prompt = CuratorSystemMessage.Render(binder);

        Assert.All(CuratorContractConstants.RelationshipKinds, kind => Assert.Contains(kind, prompt, StringComparison.Ordinal));
        Assert.All(CuratorContractConstants.TrackShapes, shape => Assert.Contains(shape, prompt, StringComparison.Ordinal));
        Assert.Contains("splitting-tracks test", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only when no Before quote shows it", prompt, StringComparison.Ordinal);
        Assert.Contains("Worked chain example", prompt, StringComparison.Ordinal);
        Assert.Contains("Worked hub example", prompt, StringComparison.Ordinal);
        Assert.Contains("fromParticipantId", prompt, StringComparison.Ordinal);
        Assert.Contains("matchEdgeIds", prompt, StringComparison.Ordinal);
        Assert.Contains("  statements, explanations      at most ", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("any text field", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultPromptReserveCoversTheRenderedDefaultPrompt()
    {
        var options = new EvidenceBinderOptions();
        var contract = new BinderContract(
            CuratorContractConstants.RelationshipKinds,
            CuratorContractConstants.TrackShapes,
            new CuratorLimits(
                options.MaximumTracks,
                options.MaximumParticipantsPerTrack,
                options.MaximumRelationshipsPerTrack,
                options.MaximumItemsPerTrack,
                options.MaximumStatementCharacters,
                CuratorContractConstants.IdFormat));

        var prompt = CuratorSystemMessage.Render(contract);

        Assert.InRange(prompt.Length, 1, options.PromptReserveCharacters);
    }

    [Fact]
    public void DefaultCompositionBudgetRegistersTheCurationGraphWithoutProviderCredentials()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddAnalysisRunServices();

        var curatorOptions = Assert.Single(
            builder.Services, descriptor => descriptor.ServiceType == typeof(CuratorOptions));
        Assert.Equal(ServiceLifetime.Singleton, curatorOptions.Lifetime);
        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(ICuratorService));
        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(ChangeLens.Core.DraftValidation.Interfaces.IDraftValidationService));
        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(ChangeLens.Core.ModelCompletion.Interfaces.IModelCompletionClient));
    }

    [Fact]
    public void InsufficientPromptReserveFailsCompositionBeforeAProviderCall()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[EvidenceBinderConfigurationConstants.PromptReserveCharactersKey] = "1";

        builder.AddAnalysisRunServices();

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(builder.Services));

        Assert.Contains("prompt reserve", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OutputReserveMustCoverTheCuratorOutputCapAndTokenEstimate()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[EvidenceBinderConfigurationConstants.CuratorOutputCharactersKey] = "10";
        builder.Configuration[CuratorConfigurationConstants.MaximumOutputCharactersKey] = "20";

        builder.AddAnalysisRunServices();

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(builder.Services));

        Assert.Contains("output", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CuratorAndValidatorKeepFullyCitedHubRelationshipsAndStripOneStrayReference()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2", "n3"]);
        var reply = "{\"thesis\":{\"text\":\"Both callers depend on one service.\",\"evidenceNodeIds\":[\"n1\",\"n2\",\"n3\"]},\"tracks\":[{\"id\":\"hub\",\"title\":\"Shared endpoint\",\"summary\":{\"text\":\"Two callers use the same service.\",\"evidenceNodeIds\":[\"n1\",\"n3\"]},\"shape\":\"ParticipantMap\",\"participants\":[{\"id\":\"callerA\",\"name\":\"Caller A\",\"role\":\"Uses service\",\"changed\":true,\"evidenceNodeIds\":[\"n1\",\"stray\"]},{\"id\":\"callerB\",\"name\":\"Caller B\",\"role\":\"Uses service\",\"changed\":false,\"evidenceNodeIds\":[\"n2\"]},{\"id\":\"service\",\"name\":\"Service\",\"role\":\"Shared endpoint\",\"changed\":true,\"evidenceNodeIds\":[\"n3\"]}],\"relationships\":[{\"id\":\"rA\",\"fromParticipantId\":\"callerA\",\"toParticipantId\":\"service\",\"kind\":\"depends-on\",\"explanation\":\"Caller A depends on service.\",\"evidenceNodeIds\":[\"n1\",\"n3\"],\"matchEdgeIds\":[]},{\"id\":\"rB\",\"fromParticipantId\":\"callerB\",\"toParticipantId\":\"service\",\"kind\":\"depends-on\",\"explanation\":\"Caller B depends on service.\",\"evidenceNodeIds\":[\"n2\",\"n3\"],\"matchEdgeIds\":[]},{\"id\":\"rBad\",\"fromParticipantId\":\"callerA\",\"toParticipantId\":\"service\",\"kind\":\"depends-on\",\"explanation\":\"Caller A cites only its spoke.\",\"evidenceNodeIds\":[\"n1\"],\"matchEdgeIds\":[]}],\"orderedSteps\":[],\"purposes\":[]}],\"droppedNodeIds\":[]}";
        var fake = new FakeModelCompletionClient(Result.Success(Completion(reply)));
        var curatorResult = await Service(fake).CurateAsync(binder, CancellationToken.None);
        var validation = new DraftValidationService().Validate(curatorResult.Data!.Draft, binder, CancellationToken.None);

        var track = Assert.Single(validation.Draft.Tracks);
        Assert.Equal(2, track.Relationships.Count);
        Assert.Contains(track.Relationships, relationship => relationship.Id == "rA");
        Assert.Contains(track.Relationships, relationship => relationship.Id == "rB");
        Assert.DoesNotContain(track.Relationships, relationship => relationship.Id == "rBad");
        Assert.Equal(["n1"], track.Participants.Single(participant => participant.Id == "callerA").EvidenceNodeIds);
        Assert.Equal(1, validation.InvalidReferenceCount);
        Assert.Equal(1, validation.UncitedEndpointCount);
    }

    private static CuratorService Service(FakeModelCompletionClient fake) => new(
        fake,
        new CuratorOptions { MaximumOutputCharacters = 10_000, MaximumOutputTokens = 1_000 },
        NullLogger<CuratorService>.Instance);

    private static ModelCompletionModel Completion(string text) =>
        new("fixture-model", text, 12.5, 20, 8, null, 1);

    private static string ValidJson() =>
        "{\"thesis\":{\"text\":\"The caller uses the service.\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[{\"id\":\"t1\",\"title\":\"Shared service\",\"summary\":{\"text\":\"The caller delegates.\",\"evidenceNodeIds\":[\"n1\"]},\"shape\":\"ParticipantMap\",\"participants\":[{\"id\":\"caller\",\"name\":\"Caller\",\"role\":\"Invokes service\",\"changed\":true,\"evidenceNodeIds\":[\"n1\"]}],\"relationships\":[],\"orderedSteps\":[],\"purposes\":[]}],\"droppedNodeIds\":[]}";
}
