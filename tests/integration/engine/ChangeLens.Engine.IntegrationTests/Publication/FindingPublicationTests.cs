using ChangeLens.Core.FindingValidation.Constants;
using ChangeLens.Core.FindingValidation.Models;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Publication.Helpers;
using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Review.Models;
using ChangeLens.Engine.IntegrationTests.Publication.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Publication;

/// <summary>Verifies how findings are attached, ordered, cited, and summarized in the reading model.</summary>
public sealed class FindingPublicationTests
{
    private static readonly CheckerFacts NoChecker = new(false, false);

    /// <summary>Verifies a finding goes to the area whose citations share the most node ids with it.</summary>
    [Fact]
    public void AttachesAFindingToTheAreaWithTheMostSharedNodes()
    {
        var reading = Build(
            [Area("a", "n1"), Area("b", "n2", "n3")],
            PublicationReview.Ran(Outcome(Finding("f1", "warning", "n1", "n2", "n3"))),
            "n1", "n2", "n3");

        Assert.Equal("b", Assert.Single(reading.Findings).AreaId);
    }

    /// <summary>Verifies an equal overlap goes to the area first in reading order.</summary>
    [Fact]
    public void TiesGoToTheFirstAreaInReadingOrder()
    {
        var reading = Build(
            [Area("a", "n1"), Area("b", "n2")],
            PublicationReview.Ran(Outcome(Finding("f1", "warning", "n2", "n1"))),
            "n1", "n2");

        Assert.Equal("a", Assert.Single(reading.Findings).AreaId);
    }

    /// <summary>Verifies a finding sharing no node with any area, or published without areas, has no area.</summary>
    [Fact]
    public void FindingsSharingNothingOrWithoutAreasHaveNoArea()
    {
        var unrelated = Build([Area("a", "n1")], PublicationReview.Ran(Outcome(Finding("f1", "warning", "n2"))), "n1", "n2");
        var withoutAreas = Build([], PublicationReview.Ran(Outcome(Finding("f1", "warning", "n1"))), "n1");

        Assert.Null(Assert.Single(unrelated.Findings).AreaId);
        Assert.Null(Assert.Single(withoutAreas.Findings).AreaId);
    }

    /// <summary>Verifies findings are ordered by severity, then area order with unattached last, then draft position.</summary>
    [Fact]
    public void OrdersBySeverityThenAreaThenDraftPosition()
    {
        var review = PublicationReview.Ran(Outcome(
            Finding("info-a", "info", "n1"),
            Finding("warning-none", "warning", "n3"),
            Finding("warning-b", "warning", "n2"),
            Finding("critical-none", "critical", "n3"),
            Finding("warning-a", "warning", "n1"),
            Finding("warning-a-later", "warning", "n1")));

        var reading = Build([Area("a", "n1"), Area("b", "n2")], review, "n1", "n2", "n3");

        Assert.Equal(
            ["critical-none", "warning-a", "warning-a-later", "warning-b", "warning-none", "info-a"],
            reading.Findings.Select(finding => finding.Id));
    }

    /// <summary>Verifies findings cite under their own claim id, unchecked, with the anchor focus and their evidence deduplicated.</summary>
    [Fact]
    public void CitesUnderTheFindingClaimIdWithTheAnchorFocus()
    {
        var reading = Build(
            [Area("a", "n1")],
            PublicationReview.Ran(Outcome(Finding("f1", "warning", "n1", "n2"), focus: new FindingFocusRange(4, 5))),
            "n1", "n2");

        var citations = reading.Citations.Where(citation => citation.ClaimId == "finding:f1").ToArray();
        Assert.Equal(["n1", "n2"], citations.Select(citation => citation.NodeId));
        Assert.All(citations, citation => Assert.Equal(CitationProvenance.Unchecked, citation.Provenance));
        Assert.Equal([new FocusRange("n1", 4, 5)], citations[0].Focus);
        Assert.Empty(citations[1].Focus);
        Assert.Equal(["n1", "n2"], reading.Evidence.Select(evidence => evidence.NodeId));
    }

    /// <summary>Verifies the recommendation follows the published severities and the withheld count.</summary>
    [Theory]
    [InlineData("critical,warning", 0, ReadingReviewRecommendation.DefectsToFix)]
    [InlineData("critical", 3, ReadingReviewRecommendation.DefectsToFix)]
    [InlineData("warning,info", 0, ReadingReviewRecommendation.IssuesWorthAddressing)]
    [InlineData("warning", 2, ReadingReviewRecommendation.IssuesWorthAddressing)]
    [InlineData("info", 0, ReadingReviewRecommendation.NoDefectsFound)]
    [InlineData("", 0, ReadingReviewRecommendation.NoDefectsFound)]
    [InlineData("info", 1, ReadingReviewRecommendation.NoDefectsConfirmed)]
    [InlineData("", 2, ReadingReviewRecommendation.NoDefectsConfirmed)]
    public void DerivesTheRecommendationForAReviewThatRan(string severities, int withheld, ReadingReviewRecommendation expected)
    {
        var findings = severities.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select((severity, index) => Finding($"f{index}", severity, "n1"))
            .ToArray();
        var validation = new FindingValidationOutcome(
            findings.Select((finding, index) => new ValidatedFinding(finding, new FindingFocusRange(1, 1), index)).ToArray(),
            Enumerable.Range(0, withheld).Select(index => new ChangeLens.Core.DraftValidation.Models.ValidationRemoval(
                FindingValidationConstants.FindingScope, $"w{index}", FindingValidationConstants.AnchorMismatchReason)).ToArray());

        var reading = Build([], PublicationReview.Ran(validation), "n1");

        Assert.Equal(ReadingReviewStatus.Ran, reading.Review.Status);
        Assert.Equal(expected, reading.Review.Recommendation);
        Assert.Equal(withheld, reading.Review.WithheldCount);
        Assert.Equal(findings.Length, reading.Findings.Count);
        Assert.DoesNotContain(reading.Assurances, assurance => assurance.Kind is ReadingAssuranceKind.ReviewNotRun
            or ReadingAssuranceKind.ReviewFailed or ReadingAssuranceKind.ReviewTooLarge);
    }

    /// <summary>Verifies a review that did not run publishes no findings, no recommendation, and its assurance.</summary>
    [Theory]
    [InlineData(ReadingReviewStatus.NotRun, ReadingAssuranceKind.ReviewNotRun)]
    [InlineData(ReadingReviewStatus.Failed, ReadingAssuranceKind.ReviewFailed)]
    [InlineData(ReadingReviewStatus.TooLarge, ReadingAssuranceKind.ReviewTooLarge)]
    public void AReviewThatDidNotRunPublishesNoFindingsOrRecommendation(ReadingReviewStatus status, ReadingAssuranceKind assurance)
    {
        var review = new PublicationReview(status, [new ValidatedFinding(Finding("f1", "critical", "n1"), new FindingFocusRange(1, 1), 0)], 4);

        var reading = Build([Area("a", "n1")], review, "n1");

        Assert.Empty(reading.Findings);
        Assert.DoesNotContain(reading.Citations, citation => citation.ClaimId.StartsWith("finding:", StringComparison.Ordinal));
        Assert.Equal(new ReadingReview(status, null, 0), reading.Review);
        Assert.Single(reading.Assurances, published => published.Kind == assurance);
    }

    private static ReadingModel Build(IReadOnlyList<MentalModelTrack> tracks, PublicationReview review, params string[] nodeIds)
    {
        var binder = PublicationTestFixtures.Binder(nodeIds);
        var graph = PublicationTestFixtures.Graph(nodeIds);
        var model = new MentalModel(null, tracks);
        var citations = CitationBuilder.Build(model, binder, graph, NoChecker);
        return ReadingModelBuilder.Build(binder.Comparison, model, citations, binder, graph, NoChecker, review);
    }

    private static MentalModelTrack Area(string id, params string[] nodeIds) => new(
        id, id, new MentalModelStatement($"{id}:summary", "Summary", nodeIds, []), "walk", [], [], [], []);

    private static FindingValidationOutcome Outcome(ReviewerFinding finding, FindingFocusRange? focus = null) =>
        Outcome([finding], focus);

    private static FindingValidationOutcome Outcome(params ReviewerFinding[] findings) => Outcome(findings, null);

    private static FindingValidationOutcome Outcome(IReadOnlyList<ReviewerFinding> findings, FindingFocusRange? focus) =>
        new(findings.Select((finding, index) => new ValidatedFinding(finding, focus ?? new FindingFocusRange(1, 1), index)).ToArray(), []);

    private static ReviewerFinding Finding(string id, string severity, params string[] nodeIds) =>
        new(id, severity, $"Title {id}", "Trigger", "Impact", "Fix", nodeIds, new ReviewerAnchor(nodeIds[0], "quote"));
}
