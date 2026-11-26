using ChangeLens.Core.Analysis.Curation.Constants;
using ChangeLens.Core.Analysis.Curation.Helpers;
using ChangeLens.Core.Analysis.Curation.Models;
using ChangeLens.Core.Analysis.Curation.Services;
using ChangeLens.Core.Analysis.DraftValidation.Services;
using ChangeLens.Core.Analysis.EvidenceBinder.Helpers;
using ChangeLens.Core.Analysis.EvidenceBinder.Models;
using ChangeLens.Core.Analysis.Shared.Constants;
using ChangeLens.Core.ModelCompletion.Constants;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.IntegrationTests.Analysis.Curation.Support;
using ChangeLens.Engine.IntegrationTests.Analysis.DraftValidation.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Curation;

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

    [Fact]
    public async Task TopLevelEchoedResponseFormatPropertyIsIgnoredWhenDraftIsValid()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2", "n3"]);
        var reply = "{\"type\":\"json_object\"," + ValidJson()[1..];
        var service = Service(new FakeModelCompletionClient(Result.Success(Completion(reply))));

        var result = await service.CurateAsync(binder, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.Diagnostics.ParseFailureReason);
        Assert.Equal("The caller uses the service.", result.Data.Draft.Thesis.Text);
        Assert.Equal("t1", Assert.Single(result.Data.Draft.Tracks).Id);
    }

    [Theory]
    [InlineData("{\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[{\"id\":\"t1\",\"title\":\"Track\",\"summary\":{\"text\":\"summary\",\"evidenceNodeIds\":[\"n1\"]},\"shape\":\"ParticipantMap\",\"participants\":[{\"id\":\"p1\",\"name\":\"Name\",\"changed\":\"true\",\"role\":\"Role\",\"evidenceNodeIds\":[\"n1\"]}],\"relationships\":[],\"orderedSteps\":[],\"purposes\":[]}],\"droppedNodeIds\":[]}")]
    [InlineData("{\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"focus\":\"claim\",\"tracks\":[],\"droppedNodeIds\":[]}")]
    [InlineData("{\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[null],\"droppedNodeIds\":[]}")]
    [InlineData("{\"type\":\"json_object\"}")]
    [InlineData("{\"type\":\"error\",\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[],\"droppedNodeIds\":[]}")]
    [InlineData("{\"type\":123,\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[],\"droppedNodeIds\":[]}")]
    [InlineData("{\"type\":\"json_object\",\"type\":\"json_object\",\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[],\"droppedNodeIds\":[]}")]
    [InlineData("{\"thesis\":{\"type\":\"json_object\",\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[],\"droppedNodeIds\":[]}")]
    [InlineData("{\"thesis\":{\"text\":\"thesis\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[{\"type\":\"json_object\",\"id\":\"t1\",\"title\":\"Track\",\"summary\":{\"text\":\"summary\",\"evidenceNodeIds\":[\"n1\"]},\"shape\":\"ParticipantMap\",\"participants\":[],\"relationships\":[],\"orderedSteps\":[],\"purposes\":[]}],\"droppedNodeIds\":[]}")]
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
        Assert.Contains("must match a cited quote character for character", prompt, StringComparison.Ordinal);
        Assert.Contains("must hold for every quoted item it covers", prompt, StringComparison.Ordinal);
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
    public async Task CuratorAndValidatorKeepFullyCitedHubRelationshipsAndStripOneStrayReference()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2", "n3"]);
        var reply = "{\"thesis\":{\"text\":\"Both callers depend on one service.\",\"evidenceNodeIds\":[\"n1\",\"n2\",\"n3\"]},\"tracks\":[{\"id\":\"hub\",\"title\":\"Shared endpoint\",\"summary\":{\"text\":\"Two callers use the same service.\",\"evidenceNodeIds\":[\"n1\",\"n3\"]},\"shape\":\"ParticipantMap\",\"participants\":[{\"id\":\"callerA\",\"name\":\"Caller A\",\"role\":\"Uses service\",\"changed\":true,\"evidenceNodeIds\":[\"n1\",\"stray\"]},{\"id\":\"callerB\",\"name\":\"Caller B\",\"role\":\"Uses service\",\"changed\":false,\"evidenceNodeIds\":[\"n2\"]},{\"id\":\"service\",\"name\":\"Service\",\"role\":\"Shared endpoint\",\"changed\":true,\"evidenceNodeIds\":[\"n3\"]}],\"relationships\":[{\"id\":\"rA\",\"fromParticipantId\":\"callerA\",\"toParticipantId\":\"service\",\"kind\":\"depends-on\",\"explanation\":\"Caller A depends on service.\",\"evidenceNodeIds\":[\"n1\",\"n3\"],\"matchEdgeIds\":[]},{\"id\":\"rB\",\"fromParticipantId\":\"callerB\",\"toParticipantId\":\"service\",\"kind\":\"depends-on\",\"explanation\":\"Caller B depends on service.\",\"evidenceNodeIds\":[\"n2\",\"n3\"],\"matchEdgeIds\":[]},{\"id\":\"rBad\",\"fromParticipantId\":\"callerA\",\"toParticipantId\":\"service\",\"kind\":\"depends-on\",\"explanation\":\"Caller A cites only its spoke.\",\"evidenceNodeIds\":[\"n1\"],\"matchEdgeIds\":[]}],\"orderedSteps\":[],\"purposes\":[]}],\"droppedNodeIds\":[]}";
        var fake = new FakeModelCompletionClient(Result.Success(Completion(reply)));
        var curatorResult = await Service(fake).CurateAsync(binder, CancellationToken.None);
        var validation = new DraftValidationService(NullLogger<DraftValidationService>.Instance)
            .Validate(curatorResult.Data!.Draft, binder, CancellationToken.None);

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
        new("fixture-model", text, 12.5, 20, 8, null, 1, false);

    private static string ValidJson() =>
        "{\"thesis\":{\"text\":\"The caller uses the service.\",\"evidenceNodeIds\":[\"n1\"]},\"tracks\":[{\"id\":\"t1\",\"title\":\"Shared service\",\"summary\":{\"text\":\"The caller delegates.\",\"evidenceNodeIds\":[\"n1\"]},\"shape\":\"ParticipantMap\",\"participants\":[{\"id\":\"caller\",\"name\":\"Caller\",\"role\":\"Invokes service\",\"changed\":true,\"evidenceNodeIds\":[\"n1\"]}],\"relationships\":[],\"orderedSteps\":[],\"purposes\":[]}],\"droppedNodeIds\":[]}";
}
