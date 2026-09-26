using ChangeLens.Core.FindingValidation.Constants;
using ChangeLens.Core.FindingValidation.Models;
using ChangeLens.Core.FindingValidation.Services;
using ChangeLens.Core.Review.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.FindingValidation;

/// <summary>
///     Verifies the ordered finding-publication rules and source focus ranges.
/// </summary>
public sealed class FindingValidationServiceTests
{
    [Fact]
    public void ValidFindingSurvivesWithAbsoluteFocusRangeAfterLineEndingAndWhitespaceNormalization()
    {
        var binder = Support.FindingValidationFixtureBinderBuilder.Create(
            Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n1", "first  \r\nbroken()\t\r\nlast", 40));
        var finding = Finding("f1", "n1", "broken()   ");

        var outcome = Validate(binder, finding);

        var valid = Assert.Single(outcome.Findings);
        Assert.Equal(0, valid.DraftPosition);
        Assert.Equal(new FindingFocusRange(41, 41), valid.FocusRange);
        Assert.Empty(outcome.Removals);
        Assert.Equal(0, outcome.WithheldCount);
    }

    [Fact]
    public void OneUndisclosedCitationRemovesTheWholeFindingAndRemovalDoesNotCopyModelText()
    {
        const string privateText = "untrusted reviewer sentence that must not enter removal records";
        var binder = Support.FindingValidationFixtureBinderBuilder.Create(
            Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n1", "alpha"));
        var finding = Finding("f1", "n1", "alpha", privateText, ["n1", "invented"]);

        var outcome = Validate(binder, finding);

        Assert.Empty(outcome.Findings);
        var removal = Assert.Single(outcome.Removals);
        Assert.Equal(FindingValidationConstants.FindingScope, removal.Scope);
        Assert.Equal("f1", removal.Id);
        Assert.Equal(FindingValidationConstants.UndisclosedEvidenceReason, removal.Reason);
        Assert.DoesNotContain(privateText, string.Join(" ", removal.Scope, removal.Id, removal.Reason), StringComparison.Ordinal);
        Assert.Equal(outcome.Removals.Count, outcome.WithheldCount);
    }

    [Fact]
    public void FindingWithoutChangedFileCitationIsRemoved()
    {
        var binder = Support.FindingValidationFixtureBinderBuilder.Create(
            Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n1", "unchanged", 10, false));

        var outcome = Validate(binder, Finding("f1", "n1", "unchanged"));

        Assert.Empty(outcome.Findings);
        Assert.Equal(FindingValidationConstants.NoChangedFileCitationReason, Assert.Single(outcome.Removals).Reason);
    }

    [Fact]
    public void MalformedIdUsesDraftPositionWhenRecordingInvalidFieldRemoval()
    {
        var binder = SourceBinder();
        var finding = Finding("bad id", "n1", "alpha");

        var outcome = Validate(binder, finding);

        Assert.Empty(outcome.Findings);
        var removal = Assert.Single(outcome.Removals);
        Assert.Equal("findings[0]", removal.Id);
        Assert.Equal(FindingValidationConstants.InvalidFieldReason, removal.Reason);
        Assert.Equal(1, outcome.WithheldCount);
    }

    [Theory]
    [InlineData("severity")]
    [InlineData("title")]
    [InlineData("evidenceNodeIds")]
    public void InvalidSeverityAndRequiredFieldLimitsRemoveTheFinding(string field)
    {
        var finding = field switch
        {
            "severity" => Finding("f1", "n1", "alpha", severity: "QUESTION"),
            "title" => Finding("f1", "n1", "alpha", title: new string('x', 121)),
            _ => Finding("f1", "n1", "alpha", evidenceNodeIds: []),
        };

        var outcome = Validate(SourceBinder(), finding);

        Assert.Empty(outcome.Findings);
        Assert.Equal(FindingValidationConstants.InvalidFieldReason, Assert.Single(outcome.Removals).Reason);
    }

    [Theory]
    [InlineData("trigger")]
    [InlineData("impact")]
    [InlineData("fix")]
    public void StatementFieldsOverTheBinderLimitAreRemoved(string field)
    {
        var oversized = new string('x', 201);
        var finding = field switch
        {
            "trigger" => Finding("f1", "n1", "alpha", trigger: oversized),
            "impact" => Finding("f1", "n1", "alpha", impact: oversized),
            _ => Finding("f1", "n1", "alpha", fix: oversized),
        };

        var outcome = Validate(SourceBinder(), finding);

        Assert.Empty(outcome.Findings);
        Assert.Equal(FindingValidationConstants.InvalidFieldReason, Assert.Single(outcome.Removals).Reason);
    }

    [Fact]
    public void WellFormedIdRemainsReservedAfterAnEarlierFindingIsRejected()
    {
        var binder = Support.FindingValidationFixtureBinderBuilder.Create(
            Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n1", "alpha", 10, false),
            Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n2", "beta", 20, true));
        var first = Finding("f1", "n1", "alpha");
        var duplicate = Finding("f1", "n2", "beta");

        var outcome = Validate(binder, first, duplicate);

        Assert.Empty(outcome.Findings);
        Assert.Equal(
            [FindingValidationConstants.NoChangedFileCitationReason, FindingValidationConstants.InvalidFieldReason],
            outcome.Removals.Select(removal => removal.Reason));
        Assert.Equal(2, outcome.WithheldCount);
    }

    [Fact]
    public void IdUniquenessIsOrdinalAndDifferentFindingsMayShareTheSameAnchor()
    {
        var binder = SourceBinder();

        var outcome = Validate(binder, Finding("f1", "n1", "alpha"), Finding("F1", "n1", "alpha"));

        Assert.Equal(2, outcome.Findings.Count);
        Assert.Empty(outcome.Removals);
    }

    [Fact]
    public void AnchorMustBeCitedAndMustMatchWholeSourceLines()
    {
        var binder = Support.FindingValidationFixtureBinderBuilder.Create(
            Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n1", "alpha beta\nsecond"),
            Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n2", "elsewhere"));
        var uncitedAnchor = Finding("f1", "n2", "elsewhere", evidenceNodeIds: ["n1"]);
        var partialLine = Finding("f2", "n1", "alpha");

        var outcome = Validate(binder, uncitedAnchor, partialLine);

        Assert.Empty(outcome.Findings);
        Assert.All(outcome.Removals, removal => Assert.Equal(FindingValidationConstants.AnchorMismatchReason, removal.Reason));
        Assert.Equal(2, outcome.WithheldCount);
    }

    [Fact]
    public void ManifestFactCannotAnchorAFinding()
    {
        var binder = Support.FindingValidationFixtureBinderBuilder.Create(
            Support.FindingValidationFixtureBinderBuilder.ManifestEvidence("m1"));

        var outcome = Validate(binder, Finding("f1", "m1", "manifest fact (Renamed) — src/old.cs → src/renamed.cs"));

        Assert.Empty(outcome.Findings);
        Assert.Equal(FindingValidationConstants.AnchorMismatchReason, Assert.Single(outcome.Removals).Reason);
    }

    [Fact]
    public void AnchorFocusRangeMustStayInsideTheDisclosedNodeRange()
    {
        var source = Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n1", "first\nsecond", 10) with { EndLine = 10 };
        var binder = Support.FindingValidationFixtureBinderBuilder.Create(source);

        var outcome = Validate(binder, Finding("f1", "n1", "second"));

        Assert.Empty(outcome.Findings);
        Assert.Equal(FindingValidationConstants.AnchorMismatchReason, Assert.Single(outcome.Removals).Reason);
    }

    [Fact]
    public void AnchorThatOccursMoreThanOnceIsAmbiguous()
    {
        var binder = Support.FindingValidationFixtureBinderBuilder.Create(
            Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n1", "alpha\nbeta\nalpha"));

        var outcome = Validate(binder, Finding("f1", "n1", "alpha"));

        Assert.Empty(outcome.Findings);
        Assert.Equal(FindingValidationConstants.AnchorAmbiguousReason, Assert.Single(outcome.Removals).Reason);
    }

    [Fact]
    public void OnlyFirstTenIncomingPositionsAreEligibleAndCapReasonTakesPrecedence()
    {
        var binder = SourceBinder();
        var findings = Enumerable.Range(0, 10).Select(index => Finding($"f{index}", "n1", "alpha")).ToList();
        findings.Add(Finding("f10", "n1", "alpha", severity: "invalid"));

        var outcome = new FindingValidationService(NullLogger<FindingValidationService>.Instance)
            .Validate(new ReviewerDraft(findings), binder, CancellationToken.None);

        Assert.Equal(10, outcome.Findings.Count);
        var removal = Assert.Single(outcome.Removals);
        Assert.Equal("f10", removal.Id);
        Assert.Equal(FindingValidationConstants.OverCapReason, removal.Reason);
        Assert.Equal(outcome.Removals.Count, outcome.WithheldCount);
    }

    private static FindingValidationOutcome Validate(
        ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder binder,
        params ReviewerFinding[] findings) =>
        new FindingValidationService(NullLogger<FindingValidationService>.Instance)
            .Validate(new ReviewerDraft(findings), binder, CancellationToken.None);

    private static ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder SourceBinder() =>
        Support.FindingValidationFixtureBinderBuilder.Create(
            Support.FindingValidationFixtureBinderBuilder.SourceEvidence("n1", "alpha"));

    private static ReviewerFinding Finding(
        string id,
        string nodeId,
        string lines,
        string impact = "The operation returns an incorrect result.",
        IReadOnlyList<string>? evidenceNodeIds = null,
        string title = "A source defect",
        string severity = "warning",
        string trigger = "A concrete input reaches this branch.",
        string fix = "Use the intended value.") => new(
            id,
            severity,
            title,
            trigger,
            impact,
            fix,
            evidenceNodeIds ?? [nodeId],
            new ReviewerAnchor(nodeId, lines));
}
