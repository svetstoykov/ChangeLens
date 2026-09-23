using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.DraftValidation.Services;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Engine.IntegrationTests.DraftValidation.Support;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.DraftValidation;

/// <summary>
///     Verifies draft validation against controlled evidence-binder fixtures.
/// </summary>
public sealed class DraftValidationServiceTests
{
    [Fact]
    public void UnknownIdsAreStrippedWhileAParticipantWithDisclosedEvidenceSurvives()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2"]);
        var draft = Draft(
            [Participant("caller", ["n1", "unknown"]), Participant("service", ["n2"])],
            summaryNodes: ["n1"],
            thesisNodes: ["n1"]);

        var outcome = Validate(draft, binder);

        Assert.True(
            outcome.Draft.Tracks.Single().Participants.Single(participant => participant.Id == "caller").EvidenceNodeIds.SequenceEqual(["n1"]));
        Assert.Equal(1, outcome.InvalidReferenceCount);
        Assert.Contains(outcome.Removals, removal => removal.Scope == "participant" && removal.Id == "caller" && removal.Reason.Contains("stripped"));
    }

    [Fact]
    public void StatementWithOnlyUndisclosedIdsIsRemoved()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1"]);
        var draft = Draft([Participant("caller", ["n1"])], summaryNodes: ["unknown"], thesisNodes: ["n1"]);

        var outcome = Validate(draft, binder);

        Assert.Empty(outcome.Draft.Tracks);
        Assert.Equal(1, outcome.InvalidReferenceCount);
        Assert.Contains(outcome.Removals, removal => removal.Scope == "statement" && removal.Id == "track:t1:summary");
    }

    [Fact]
    public void ChildrenOfATrackWithNoSurvivingParticipantsAreNotInspected()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1"]);
        var draft = Draft(
            [Participant("bad", ["n1"], name: "")],
            [Relationship("r1", "bad", "missing", ["unknown"], "unknown-kind")],
            summaryNodes: ["n1"],
            thesisNodes: ["n1"],
            orderedSteps: [Statement("step", ["unknown"])],
            purposes: [Statement("purpose", ["unknown"])]);

        var outcome = Validate(draft, binder);

        Assert.Empty(outcome.Draft.Tracks);
        Assert.Equal(0, outcome.InvalidReferenceCount);
        Assert.Equal(0, outcome.InvalidKindCount);
        Assert.Equal(0, outcome.UncitedEndpointCount);
        Assert.DoesNotContain(outcome.Removals, removal => removal.Scope == "relationship");
        Assert.DoesNotContain(outcome.Removals, removal => removal.Scope == "statement" && removal.Id.Contains("step", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidKindIsNotCountedWhenRelationshipHasNoDisclosedEvidence()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2"]);
        var draft = Draft(
            [Participant("from", ["n1"]), Participant("to", ["n2"])],
            [Relationship("r1", "from", "to", ["unknown"], "unknown-kind")],
            summaryNodes: ["n1"],
            thesisNodes: ["n1"]);

        var outcome = Validate(draft, binder);

        Assert.Empty(outcome.Draft.Tracks.Single().Relationships);
        Assert.Equal(1, outcome.InvalidReferenceCount);
        Assert.Equal(0, outcome.InvalidKindCount);
    }

    [Fact]
    public void TitlesNamesAndRolesAreRequiredButDoNotUseTheStatementCharacterCap()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(
            ["n1"],
            limits: new CuratorLimits(8, 8, 8, 8, 3, CuratorContractConstants.IdFormat));
        var draft = new MentalModelDraft(
            Statement("t", ["n1"]),
            [new DraftTrack(
                "t1",
                "Title",
                Statement("s", ["n1"]),
                "ParticipantMap",
                [Participant("p1", ["n1"], name: "Name", role: "Role")],
                [],
                [],
                [])],
            []);

        var outcome = Validate(draft, binder);

        var track = Assert.Single(outcome.Draft.Tracks);
        Assert.Equal("Title", track.Title);
        Assert.Equal("Name", Assert.Single(track.Participants).Name);
        Assert.Equal("Role", Assert.Single(track.Participants).Role);
    }

    [Fact]
    public void HubRelationshipsSurviveWhenEachCitesBothEndpoints()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2", "n3"]);
        var draft = Draft(
            [Participant("caller", ["n1"]), Participant("service", ["n2"]), Participant("other", ["n3"])],
            [
                Relationship("r1", "caller", "service", ["n1", "n2"]),
                Relationship("r2", "other", "service", ["n3", "n2"]),
            ],
            summaryNodes: ["n1"],
            thesisNodes: ["n1"]);

        var outcome = Validate(draft, binder);

        Assert.Equal(2, outcome.Draft.Tracks.Single().Relationships.Count);
        Assert.Equal(0, outcome.UncitedEndpointCount);
    }

    [Fact]
    public void HubRelationshipCitingOnlyTheSpokeIsRemovedAsUncitedEndpoint()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2"]);
        var draft = Draft(
            [Participant("caller", ["n1"]), Participant("service", ["n2"])],
            [Relationship("r1", "caller", "service", ["n1"])],
            summaryNodes: ["n1"],
            thesisNodes: ["n1"]);

        var outcome = Validate(draft, binder);

        Assert.Empty(outcome.Draft.Tracks.Single().Relationships);
        Assert.Equal(1, outcome.UncitedEndpointCount);
        Assert.Contains(outcome.Removals, removal => removal.Reason.Contains("to endpoint 'service'"));
        Assert.Equal(0, outcome.InvalidReferenceCount);
    }

    [Fact]
    public void SelfLoopRelationshipIsRemovedWithoutCountingAsAnUncitedEndpoint()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1"]);
        var draft = Draft(
            [Participant("caller", ["n1"])],
            [Relationship("r1", "caller", "caller", ["n1"])],
            summaryNodes: ["n1"],
            thesisNodes: ["n1"]);

        var outcome = Validate(draft, binder);

        Assert.Equal("caller", Assert.Single(outcome.Draft.Tracks.Single().Participants).Id);
        Assert.Empty(outcome.Draft.Tracks.Single().Relationships);
        Assert.Equal(0, outcome.UncitedEndpointCount);
        Assert.Equal(0, outcome.InvalidReferenceCount);
        Assert.Contains(
            outcome.Removals,
            removal => removal.Scope == "relationship"
                && removal.Id == "r1"
                && removal.Reason.Contains("same participant", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidKindIsCountedEvenWhenTheRelationshipIsRemoved()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2"]);
        var draft = Draft(
            [Participant("caller", ["n1"]), Participant("service", ["n2"])],
            [Relationship("r1", "caller", "missing", ["n1", "n2"], "unknown-kind")],
            summaryNodes: ["n1"],
            thesisNodes: ["n1"]);

        var outcome = Validate(draft, binder);

        Assert.Empty(outcome.Draft.Tracks.Single().Relationships);
        Assert.Equal(1, outcome.InvalidKindCount);
        Assert.Contains(outcome.Removals, removal => removal.Reason.Contains("surviving participant"));
    }

    [Fact]
    public void SupersedesRequiresAnEdgePathAndASharedNodeAloneIsNotAPath()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "shared", "n2"]);
        var draft = Draft(
            [Participant("from", ["n1", "shared"]), Participant("to", ["shared", "n2"])],
            [Relationship("r1", "from", "to", ["n1", "shared", "n2"], "supersedes")],
            summaryNodes: ["n1"],
            thesisNodes: ["n1"]);

        var removed = Validate(draft, binder);
        Assert.Empty(removed.Draft.Tracks.Single().Relationships);

        var withPath = DraftValidationFixtureBinderBuilder.Create(["n1", "shared", "n2"], [("edge", "n1", "n2")]);
        var kept = Validate(draft, withPath);
        Assert.Single(kept.Draft.Tracks.Single().Relationships);
    }

    [Fact]
    public void CapsApplyToIncomingPositionsAndValidRejectedIdsReserveUniqueness()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(
            ["n1", "n2", "n3", "n4"],
            limits: new CuratorLimits(8, 5, 8, 1, 200, "fixture"));
        var draft = Draft(
            [
                Participant("reserved", ["n1"], name: ""),
                Participant("reserved", ["n2"]),
                Participant("ok", ["n3"]),
                Participant("bad/id", ["n4"]),
                Participant("bad/id", ["n4"]),
            ],
            summaryNodes: ["n1"],
            thesisNodes: ["n1"],
            orderedSteps: [Statement("step", ["n1"]), Statement("over-cap", ["n2"])],
            purposes: [Statement("purpose", ["n1"]), Statement("over-cap-purpose", ["n2"]) ]);

        var outcome = Validate(draft, binder);
        var track = outcome.Draft.Tracks.Single();

        Assert.Single(track.Participants);
        Assert.Equal("bad/id", outcome.Removals.First(removal => removal.Scope == "participant" && removal.Id == "bad/id").Id);
        Assert.Equal(2, track.OrderedSteps.Count + track.Purposes.Count);
        Assert.Contains(outcome.Removals, removal => removal.Reason.Contains("beyond the 1-item limit"));
        Assert.Contains(outcome.Removals, removal => removal.Scope == "participant" && removal.Reason.Contains("duplicate participant"));
        Assert.Equal(0, outcome.InvalidReferenceCount);
    }

    [Fact]
    public void UndeclaredAccountingIncludesCitationsInRejectedAndOverCapItems()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(
            ["n1", "n2", "n3"],
            limits: new CuratorLimits(8, 2, 8, 8, 200, "fixture"));
        var draft = Draft(
            [
                Participant("invalid", ["n1"], name: ""),
                Participant("kept", ["n2"]),
                Participant("over-cap", ["n3"]),
            ],
            summaryNodes: ["n2"],
            thesisNodes: ["n2"]);

        var outcome = Validate(draft, binder);

        Assert.Equal(0, outcome.UndeclaredNodeCount);
        Assert.Single(outcome.Draft.Tracks.Single().Participants);
    }

    [Fact]
    public void UnknownReferencesAreDistinctPerInspectedListAndBothUncitedEndpointsCountOnce()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2", "n3"]);
        var draft = Draft(
            [Participant("from", ["x", "x", "n1"]), Participant("to", ["n2"])],
            [
                Relationship("r1", "from", "to", ["y", "y", "n3"], matchEdgeIds: ["edge-x", "edge-x"]),
                Relationship("r2", "from", "to", ["n3"]),
            ],
            summaryNodes: ["x", "n1"],
            thesisNodes: ["x", "n1"],
            droppedNodeIds: ["dropped-x", "dropped-x"]);

        var outcome = Validate(draft, binder);

        Assert.Equal(6, outcome.InvalidReferenceCount);
        Assert.Equal(1, outcome.UncitedEndpointCount);
        Assert.Empty(outcome.Draft.Tracks.Single().Relationships);
    }

    [Fact]
    public void UnknownDroppedIdsAreStrippedAndCountedWithoutARemoval()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1", "n2"]);
        var draft = Draft([Participant("caller", ["n1"])], summaryNodes: ["n1"], thesisNodes: ["n1"], droppedNodeIds: ["n2", "unknown-drop"]);

        var outcome = Validate(draft, binder);

        Assert.True(outcome.Draft.DroppedNodeIds.SequenceEqual(["n2"]));
        Assert.Equal(1, outcome.InvalidReferenceCount);
        Assert.Empty(outcome.Removals);
    }

    [Fact]
    public void RejectedThesisIsRecordedOnceAsAStatementRemoval()
    {
        var binder = DraftValidationFixtureBinderBuilder.Create(["n1"]);
        var draft = Draft([Participant("caller", ["n1"])], summaryNodes: ["n1"], thesisNodes: ["unknown"]);

        var outcome = Validate(draft, binder);

        var removal = Assert.Single(outcome.Removals);
        Assert.Equal("statement", removal.Scope);
        Assert.Equal("thesis", removal.Id);
    }

    private static DraftValidationService Service() => new();

    private static DraftValidationOutcome Validate(MentalModelDraft draft, EvidenceBinderModel binder) =>
        Service().Validate(draft, binder, CancellationToken.None);

    private static MentalModelDraft Draft(
        IReadOnlyList<DraftParticipant> participants,
        IReadOnlyList<DraftRelationship>? relationships = null,
        IReadOnlyList<string>? summaryNodes = null,
        IReadOnlyList<string>? thesisNodes = null,
        IReadOnlyList<BoundStatement>? orderedSteps = null,
        IReadOnlyList<BoundStatement>? purposes = null,
        IReadOnlyList<string>? droppedNodeIds = null) => new(
        Statement("thesis", thesisNodes ?? ["n1"]),
        [new DraftTrack(
            "t1",
            "Track",
            Statement("summary", summaryNodes ?? ["n1"]),
            "ParticipantMap",
            participants,
            relationships ?? [],
            orderedSteps ?? [],
            purposes ?? [])],
        droppedNodeIds ?? []);

    private static BoundStatement Statement(string text, IReadOnlyList<string> evidenceNodeIds) => new(text, evidenceNodeIds);

    private static DraftParticipant Participant(
        string id,
        IReadOnlyList<string> evidenceNodeIds,
        string name = "Name",
        string role = "Role") => new(id, name, role, true, evidenceNodeIds);

    private static DraftRelationship Relationship(
        string id,
        string from,
        string to,
        IReadOnlyList<string> evidenceNodeIds,
        string kind = "depends-on",
        IReadOnlyList<string>? matchEdgeIds = null) =>
        new(id, from, to, kind, "Explanation", evidenceNodeIds, matchEdgeIds ?? []);
}
