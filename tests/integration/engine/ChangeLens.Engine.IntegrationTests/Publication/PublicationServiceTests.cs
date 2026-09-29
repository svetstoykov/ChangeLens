using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.ClaimChecking.Services;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.DraftValidation.Services;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceFrontier.Constants;
using ChangeLens.Core.EvidenceFrontier.Models;
using ChangeLens.Core.EvidenceFrontier.Services;
using ChangeLens.Core.FindingValidation.Models;
using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Publication.Services;
using ChangeLens.Core.Review.Models;
using ChangeLens.Engine.IntegrationTests.ClaimChecking.Support;
using ChangeLens.Engine.IntegrationTests.DraftValidation.Support;
using ChangeLens.Engine.IntegrationTests.Publication.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Engine.IntegrationTests.Publication;

/// <summary>Verifies publication orchestration around the default and checked paths.</summary>
public sealed class PublicationServiceTests
{
    /// <summary>Verifies checker-off publication remains unchecked and does not create bound-not-used entries.</summary>
    [Fact]
    public async Task CheckerOffPublishesValidatedDraftWithUncheckedCitations()
    {
        var checking = new RecordingClaimCheckingService(new ClaimCheckingOutcome(
            ChangeLens.Core.MentalModels.Models.MentalModel.Empty, PublicationTestFixtures.Summary()));
        var frontier = new RecordingFrontierService();
        var service = new PublicationService(checking, frontier, new ClaimCheckingOptions(),
            NullLogger<PublicationService>.Instance);
        var binder = PublicationTestFixtures.Binder("n1");
        var request = new PublicationRequest(
            PublicationTestFixtures.Validation("n1"), binder, PublicationTestFixtures.Graph("n1"),
            PublicationTestFixtures.Policy(), PublicationTestFixtures.Ranking(),
            PublicationReview.NotRun);

        var result = await service.PublishAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(checking.Called);
        Assert.Null(frontier.UsedNodeIds);
        Assert.NotEmpty(result.Data!.ReadingModel.Citations);
        Assert.All(result.Data.ReadingModel.Citations, citation => Assert.Equal(CitationProvenance.Unchecked, citation.Provenance));
        Assert.DoesNotContain(result.Data.EvidenceFrontier.Entries, entry => entry.OmissionKind == FrontierOmissionKind.BoundNotUsed);
    }

    /// <summary>Verifies a removed checked claim leaves its bound quote available to the frontier.</summary>
    [Fact]
    public async Task RemovedClaimIsAbsentAndItsQuoteReachesFrontier()
    {
        var checking = new RecordingClaimCheckingService(new ClaimCheckingOutcome(
            ChangeLens.Core.MentalModels.Models.MentalModel.Empty, PublicationTestFixtures.Summary()));
        var frontier = new RecordingFrontierService();
        var service = new PublicationService(checking, frontier, new ClaimCheckingOptions { Enabled = true },
            NullLogger<PublicationService>.Instance, new RecordingClaimChecker());
        var binder = PublicationTestFixtures.Binder("n1");
        var request = new PublicationRequest(
            PublicationTestFixtures.Validation("n1"), binder, PublicationTestFixtures.Graph("n1"),
            PublicationTestFixtures.Policy(), PublicationTestFixtures.Ranking(),
            PublicationReview.NotRun);

        var result = await service.PublishAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(checking.Called);
        Assert.Empty(result.Data!.ReadingModel.Areas);
        Assert.Contains(result.Data.EvidenceFrontier.Entries,
            entry => entry.NodeId == "n1" && entry.OmissionKind == FrontierOmissionKind.BoundNotUsed);
    }

    /// <summary>Verifies unchecked participant citations do not count as checker-used evidence.</summary>
    [Fact]
    public async Task ParticipantCitationsDoNotShrinkFrontier()
    {
        var checkedModel = new ChangeLens.Core.MentalModels.Models.MentalModel(null, [
            new ChangeLens.Core.MentalModels.Models.MentalModelTrack(
                "track", "Track", null, "ParticipantList",
                [new ChangeLens.Core.Curation.Models.DraftParticipant("participant", "Participant", "role", true, ["n1"])],
                [], [], []),
        ]);
        var checking = new RecordingClaimCheckingService(new ClaimCheckingOutcome(checkedModel, PublicationTestFixtures.Summary()));
        var frontier = new RecordingFrontierService();
        var service = new PublicationService(checking, frontier, new ClaimCheckingOptions { Enabled = true },
            NullLogger<PublicationService>.Instance, new RecordingClaimChecker());
        var binder = PublicationTestFixtures.Binder("n1");
        var request = new PublicationRequest(
            PublicationTestFixtures.Validation("n1"), binder, PublicationTestFixtures.Graph("n1"),
            PublicationTestFixtures.Policy(), PublicationTestFixtures.Ranking(),
            PublicationReview.NotRun);

        var result = await service.PublishAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(CitationProvenance.Unchecked, Assert.Single(result.Data!.ReadingModel.Citations).Provenance);
        Assert.Contains(result.Data.EvidenceFrontier.Entries,
            entry => entry.NodeId == "n1" && entry.OmissionKind == FrontierOmissionKind.BoundNotUsed);
    }

    /// <summary>
    ///     Verifies the published explanation rebuilds a reading model without checking again, dropping the whole review contribution.
    /// </summary>
    [Fact]
    public async Task RebuildFromThePublishedExplanationDoesNotCheckAgainAndDropsTheReview()
    {
        var checkedModel = new ChangeLens.Core.MentalModels.Models.MentalModel(null, [
            new ChangeLens.Core.MentalModels.Models.MentalModelTrack(
                "track", "Track", new ChangeLens.Core.MentalModels.Models.MentalModelStatement("track:summary", "Summary", ["n1"], []),
                CuratorContractConstants.Walk, [], [], [], []),
        ]);
        var checking = new RecordingClaimCheckingService(new ClaimCheckingOutcome(checkedModel, PublicationTestFixtures.Summary()));
        var service = new PublicationService(checking, new RecordingFrontierService(), new ClaimCheckingOptions { Enabled = true },
            NullLogger<PublicationService>.Instance, new RecordingClaimChecker());
        var binder = PublicationTestFixtures.Binder("n1", "n2");
        var graph = PublicationTestFixtures.Graph("n1", "n2");
        var finding = new ReviewerFinding("f1", "warning", "Title", "Trigger", "Impact", "Fix", ["n2"], new ReviewerAnchor("n2", "quote"));
        var review = PublicationReview.Ran(new FindingValidationOutcome([new ValidatedFinding(finding, new FindingFocusRange(1, 1), 0)], []));
        var request = new PublicationRequest(
            PublicationTestFixtures.Validation("n1"), binder, graph, PublicationTestFixtures.Policy(), PublicationTestFixtures.Ranking(), review);

        var published = await service.PublishAsync(request, TestContext.Current.CancellationToken);
        var rebuilt = service.Rebuild(published.Data!.Explanation, binder, graph, PublicationReview.TooLarge);

        var original = published.Data.ReadingModel;
        Assert.Equal(1, checking.CallCount);
        Assert.Contains(original.Evidence, evidence => evidence.NodeId == "n2");
        Assert.Single(original.Findings);
        Assert.Empty(rebuilt.Findings);
        Assert.Equal(new ReadingReview(ReadingReviewStatus.TooLarge, null, 0), rebuilt.Review);
        Assert.Equal(["n1"], rebuilt.Evidence.Select(evidence => evidence.NodeId));
        Assert.DoesNotContain(rebuilt.Citations, citation => citation.ClaimId.StartsWith("finding:", StringComparison.Ordinal));
        Assert.Equal(original.Citations.Where(citation => !citation.ClaimId.StartsWith("finding:", StringComparison.Ordinal)),
            rebuilt.Citations);
        Assert.Contains(rebuilt.Assurances, assurance => assurance.Kind == ReadingAssuranceKind.ReviewTooLarge);
        Assert.Equal(original.Assurances.Count(assurance => assurance.Kind != ReadingAssuranceKind.ReviewTooLarge),
            rebuilt.Assurances.Count - 1);
    }

    /// <summary>Verifies duplicate claim citations withheld by publication do not count as checked frontier usage.</summary>
    [Fact]
    public async Task DuplicateClaimCitationsDoNotShrinkFrontier()
    {
        var duplicate = new ChangeLens.Core.MentalModels.Models.MentalModelStatement("duplicate", "Duplicate", ["n1"], []);
        var checkedModel = new ChangeLens.Core.MentalModels.Models.MentalModel(null, [
            new ChangeLens.Core.MentalModels.Models.MentalModelTrack("one", "One", duplicate, CuratorContractConstants.Walk, [], [], [], []),
            new ChangeLens.Core.MentalModels.Models.MentalModelTrack(
                "two",
                "Two",
                duplicate with { EvidenceNodeIds = ["n2"] },
                CuratorContractConstants.Walk,
                [],
                [],
                [],
                []),
        ]);
        var checking = new RecordingClaimCheckingService(new ClaimCheckingOutcome(checkedModel, PublicationTestFixtures.Summary()));
        var frontier = new RecordingFrontierService();
        var service = new PublicationService(checking, frontier, new ClaimCheckingOptions { Enabled = true },
            NullLogger<PublicationService>.Instance, new RecordingClaimChecker());
        var binder = PublicationTestFixtures.Binder("n1", "n2");
        var request = new PublicationRequest(
            PublicationTestFixtures.Validation("n1"), binder, PublicationTestFixtures.Graph("n1", "n2"),
            PublicationTestFixtures.Policy(), PublicationTestFixtures.Ranking(),
            PublicationReview.NotRun);

        var result = await service.PublishAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.ReadingModel.Citations);
        Assert.Empty(frontier.UsedNodeIds!);
        Assert.Contains(result.Data.EvidenceFrontier.Entries,
            entry => entry.NodeId == "n1" && entry.OmissionKind == FrontierOmissionKind.BoundNotUsed);
        Assert.Contains(result.Data.EvidenceFrontier.Entries,
            entry => entry.NodeId == "n2" && entry.OmissionKind == FrontierOmissionKind.BoundNotUsed);
    }

    /// <summary>
    ///     Verifies the checked path through the real checking and frontier services: claims are Checked, the thesis is
    ///     Derived, participants stay Unchecked, and only non-Unchecked citations count as used.
    /// </summary>
    [Fact]
    public async Task CheckedPathThroughRealServicesPublishesDerivedThesis()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2", "n3"]);
        var draft = Draft([Track("t1", "Summary one", ["n1"], [Participant("p1", ["n1"]), Participant("p2", ["n2"])],
            [new BoundStatement("First", ["n1"]), new BoundStatement("Second", ["n2"])])]);
        var validation = Validate(draft, binder);
        var checker = new RecordingClaimChecker(claims => new ClaimCheckerReply(claims.Select(claim => new ClaimVerdict(
            claim.ClaimId,
            claim.ClaimId.EndsWith(":step:1", StringComparison.Ordinal) ? ClaimVerdictKind.Unsupported : ClaimVerdictKind.Supported,
            null,
            [])).ToArray(), null));
        var service = new PublicationService(new ClaimCheckingService(checker), new EvidenceFrontierService(new EvidenceFrontierOptions()),
            new ClaimCheckingOptions { Enabled = true }, NullLogger<PublicationService>.Instance, checker);

        var result = await service.PublishAsync(
            new PublicationRequest(validation, binder, PublicationTestFixtures.Graph("n1", "n2", "n3"), PublicationTestFixtures.Policy(),
                PublicationTestFixtures.Ranking(), PublicationReview.NotRun),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var reading = result.Data!.ReadingModel;
        Assert.Equal(ReadingTrust.Derived, reading.Thesis!.Trust);
        Assert.All(reading.Citations.Where(citation => citation.ClaimId.Contains(":step:") || citation.ClaimId.EndsWith(":summary")),
            citation => Assert.Equal(CitationProvenance.Checked, citation.Provenance));
        Assert.All(reading.Citations.Where(citation => citation.ClaimId.Contains(":participant:")),
            citation => Assert.Equal(CitationProvenance.Unchecked, citation.Provenance));
        Assert.DoesNotContain(reading.Assurances, assurance => assurance.Kind == ReadingAssuranceKind.CheckerNotRun);
        var boundNotUsed = result.Data.EvidenceFrontier.Entries
            .Where(entry => entry.OmissionKind == FrontierOmissionKind.BoundNotUsed)
            .Select(entry => entry.NodeId)
            .Order(StringComparer.Ordinal);
        Assert.Equal(["n2", "n3"], boundNotUsed);
        Assert.Equal(1, result.Data.EvidenceFrontier.Diagnostics.UsedNodeCount);
    }

    private static DraftValidationOutcome Validate(MentalModelDraft draft, EvidenceBinderModel binder) =>
        new DraftValidationService().Validate(draft, binder, TestContext.Current.CancellationToken);

    private static MentalModelDraft Draft(IReadOnlyList<DraftTrack> tracks) => new(new BoundStatement("Thesis", ["n1"]), tracks, []);

    private static DraftTrack Track(
        string id,
        string summary,
        IReadOnlyList<string> summaryNodes,
        IReadOnlyList<DraftParticipant> participants,
        IReadOnlyList<BoundStatement> steps) =>
        new(id, id, new BoundStatement(summary, summaryNodes), CuratorContractConstants.Walk, participants, [], steps, []);

    private static DraftParticipant Participant(string id, IReadOnlyList<string> nodes) => new(id, id, "Role", true, nodes);
}
