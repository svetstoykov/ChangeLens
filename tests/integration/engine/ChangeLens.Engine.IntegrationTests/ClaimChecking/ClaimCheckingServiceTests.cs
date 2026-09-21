using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.ClaimChecking.Services;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.DraftValidation.Services;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.MentalModels.Helpers;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.IntegrationTests.ClaimChecking.Support;
using ChangeLens.Engine.IntegrationTests.DraftValidation.Support;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.ClaimChecking;

/// <summary>
///     Verifies deterministic claim construction and verdict application.
/// </summary>
public sealed class ClaimCheckingServiceTests
{
    [Fact]
    public async Task RelationshipClaimCarriesCompleteExplanationAndEndpointRoles()
    {
        var binder = Binder(["n1", "n2"]);
        var validation = Validate(
            Draft(relationships: [Relationship("r1", "p1", "p2", ["n1", "n2"], "invokes", "The complete explanation.")]),
            binder);
        var checker = new RecordingClaimChecker(claims => new ClaimCheckerReply(Supported(claims), null));

        var result = await Service(checker).CheckAsync(validation, binder, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var claim = Assert.Single(checker.Claims, item => item.Type == "relationship");
        Assert.Contains("Explanation: The complete explanation.", claim.Text, StringComparison.Ordinal);
        Assert.Equal("From (Role)", claim.From);
        Assert.Equal("To (Role)", claim.To);
        Assert.Equal("from", claim.Quotes[0].Role);
        Assert.Equal("to", claim.Quotes[1].Role);
    }

    [Fact]
    public async Task UnsupportedRelationshipIsRemovedAndSupportedRelationshipKeepsExplanation()
    {
        var binder = Binder(["n1", "n2"]);
        var validation = Validate(Draft(relationships: [Relationship("r1", "p1", "p2", ["n1", "n2"])]), binder);
        var unsupported = new RecordingClaimChecker(claims => new ClaimCheckerReply(
            claims.Select(claim => claim.Type == "relationship" ? new ClaimVerdict(claim.ClaimId, ClaimVerdictKind.Unsupported, null, []) :
                new ClaimVerdict(claim.ClaimId, ClaimVerdictKind.Supported, null, [])).ToArray(), null));

        var removed = await Service(unsupported).CheckAsync(validation, binder, CancellationToken.None);

        Assert.True(removed.IsSuccess);
        Assert.Empty(removed.Data!.Model.Tracks);
        Assert.Contains(removed.Data.Summary.RemovedClaimIds, id => id.EndsWith(":r1", StringComparison.Ordinal));

        var supported = new RecordingClaimChecker(claims => new ClaimCheckerReply(Supported(claims), null));
        var retained = await Service(supported).CheckAsync(validation, binder, CancellationToken.None);
        Assert.Equal("Explanation", Assert.Single(retained.Data!.Model.Tracks).Relationships.Single().Explanation);
    }

    [Fact]
    public async Task TwoDisjointFocusRangesAreRetainedAndInvalidRangeIsDropped()
    {
        var binder = Binder(["n1"]) with
        {
            Evidence = [BinderEvidence("n1", 10, 42, string.Join('\n', Enumerable.Range(10, 33).Select(line => $"line {line}")))],
        };
        var validation = Validate(Draft(steps: [new BoundStatement("Step", ["n1"])]), binder);
        var checker = new RecordingClaimChecker(claims => new ClaimCheckerReply(
            claims.Select(claim => new ClaimVerdict(
                claim.ClaimId,
                ClaimVerdictKind.Supported,
                null,
                claim.Type == "step"
                    ? [new FocusRange("n1", 10, 12), new FocusRange("n1", 10, 12), new FocusRange("n1", 40, 42), new FocusRange("n1", 99, 100)]
                    : [])).ToArray(), null));

        var result = await Service(checker).CheckAsync(validation, binder, CancellationToken.None);

        var step = Assert.Single(Assert.Single(result.Data!.Model.Tracks).OrderedSteps);
        Assert.Equal([new FocusRange("n1", 10, 12), new FocusRange("n1", 40, 42)], step.Focus);
        Assert.Equal(1, result.Data.Summary.DroppedRangeCount);
        Assert.Equal(1, result.Data.Summary.NarrowedCount);
    }

    [Fact]
    public async Task FullyResolvableDisjointFocusRangesAreNotCountedAsNarrowed()
    {
        var binder = Binder(["n1"]) with
        {
            Evidence = [BinderEvidence("n1", 10, 42, string.Join('\n', Enumerable.Range(10, 33).Select(line => $"line {line}")))],
        };
        var validation = Validate(Draft(steps: [new BoundStatement("Step", ["n1"])]), binder);
        var checker = new RecordingClaimChecker(claims => new ClaimCheckerReply(
            claims.Select(claim => new ClaimVerdict(
                claim.ClaimId,
                ClaimVerdictKind.Supported,
                null,
                claim.Type == "step" ? [new FocusRange("n1", 10, 12), new FocusRange("n1", 40, 42)] : [])).ToArray(), null));

        var result = await Service(checker).CheckAsync(validation, binder, CancellationToken.None);

        Assert.Equal(0, result.Data!.Summary.NarrowedCount);
        Assert.Equal([new FocusRange("n1", 10, 12), new FocusRange("n1", 40, 42)],
            Assert.Single(Assert.Single(result.Data.Model.Tracks).OrderedSteps).Focus);
    }

    [Fact]
    public async Task CompletelyUnresolvableFocusRemovesTheClaimInsteadOfClampingIt()
    {
        var binder = Binder(["n1"]);
        var validation = Validate(Draft(steps: [new BoundStatement("Step", ["n1"])]), binder);
        var checker = new RecordingClaimChecker(claims => new ClaimCheckerReply(
            claims.Select(claim => new ClaimVerdict(
                claim.ClaimId,
                ClaimVerdictKind.Supported,
                null,
                claim.Type == "step" ? [new FocusRange("n1", 20, 20)] : [])).ToArray(), null));

        var result = await Service(checker).CheckAsync(validation, binder, CancellationToken.None);

        Assert.Empty(result.Data!.Model.Tracks);
        Assert.Equal(1, result.Data.Summary.UnaddressableFocusCount);
        Assert.Contains(result.Data.Summary.RemovedClaimIds, id => id.EndsWith(":step:0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateUnknownAndMissingVerdictsAreCountedAndNeverPublished()
    {
        var binder = Binder(["n1", "n2"]);
        var validation = Validate(Draft(steps: [new BoundStatement("Step 1", ["n1"]), new BoundStatement("Step 2", ["n2"])]), binder);
        var checker = new RecordingClaimChecker(claims =>
        {
            var summary = claims.Single(claim => claim.Type == "summary");
            return new ClaimCheckerReply([
                new ClaimVerdict(summary.ClaimId, ClaimVerdictKind.Supported, null, []),
                new ClaimVerdict(summary.ClaimId, ClaimVerdictKind.Supported, null, []),
                new ClaimVerdict("unknown", ClaimVerdictKind.Supported, null, []),
            ], null);
        });

        var result = await Service(checker).CheckAsync(validation, binder, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Model.Tracks);
        Assert.Equal(1, result.Data.Summary.UnknownVerdictCount);
        Assert.Equal(3, result.Data.Summary.UncheckedCount);
        Assert.Equal(3, result.Data.Summary.UncheckedClaimIds.Count);
    }

    [Fact]
    public async Task CorrectedSupersedesRequiresAMatchPathAndClearsMatchEdges()
    {
        var draft = Draft(relationships: [Relationship("r1", "p1", "p2", ["n1", "n2"], "invokes", "Original")]);
        var noPathBinder = Binder(["n1", "n2"]);
        var noPathValidation = Validate(draft, noPathBinder);
        var noPathChecker = CorrectionChecker("supersedes");
        var refused = await Service(noPathChecker).CheckAsync(noPathValidation, noPathBinder, CancellationToken.None);
        Assert.Empty(refused.Data!.Model.Tracks);
        Assert.Equal(1, refused.Data.Summary.RefusedCorrectionCount);

        var pathBinder = Binder(["n1", "n2"], [("e1", "n1", "n2")]);
        var pathValidation = Validate(draft, pathBinder);
        var corrected = await Service(CorrectionChecker("supersedes")).CheckAsync(pathValidation, pathBinder, CancellationToken.None);
        var relationship = Assert.Single(Assert.Single(corrected.Data!.Model.Tracks).Relationships);
        Assert.Equal("supersedes", relationship.Kind);
        Assert.Empty(relationship.MatchEdgeIds);
        Assert.Contains("curator's explanation described a invokes relationship", relationship.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParticipantAndOriginalClaimIdsSurviveCompactionAndThesisUsesHeadlines()
    {
        var binder = Binder(["n1", "n2", "n3"]);
        var validation = Validate(Draft(
            participants: [Participant("p1", ["n1"]), Participant("p2", ["n2"]), Participant("unused", ["n3"])],
            steps: [new BoundStatement("First", ["n1"]), new BoundStatement("Second", ["n2"])]), binder);
        var checker = new RecordingClaimChecker(claims => new ClaimCheckerReply(
            claims.Where(claim => !claim.ClaimId.EndsWith(":step:0", StringComparison.Ordinal)).Select(claim =>
                new ClaimVerdict(claim.ClaimId, ClaimVerdictKind.Supported, null, [])).ToArray(), null));

        var result = await Service(checker).CheckAsync(validation, binder, CancellationToken.None);

        var track = Assert.Single(result.Data!.Model.Tracks);
        Assert.Equal("track:t1:step:1", Assert.Single(track.OrderedSteps).ClaimId);
        Assert.Equal(["p1", "p2"], track.Participants.Select(participant => participant.Id));
        Assert.Equal("Summary", result.Data.Model.Thesis!.Text);
        Assert.Equal("thesis", result.Data.Model.Thesis.ClaimId);
    }

    [Fact]
    public async Task ParseFailurePublishesEmptyModelAndPortFailureIsForwarded()
    {
        var binder = Binder(["n1"]);
        var validation = Validate(Draft(), binder);
        var parse = await Service(new RecordingClaimChecker(_ => new ClaimCheckerReply([], "invalid JSON")))
            .CheckAsync(validation, binder, CancellationToken.None);
        Assert.True(parse.IsSuccess);
        Assert.Empty(parse.Data!.Model.Tracks);
        Assert.Equal("invalid JSON", parse.Data.Summary.ParseFailure);

        var error = OperationError.ExternalDependencyFailure("checker unavailable", "checker.failure");
        var failed = await Service(new RecordingClaimChecker(result: Result.Fail<ClaimCheckerReply>(error)))
            .CheckAsync(validation, binder, CancellationToken.None);
        Assert.True(failed.IsFailure);
        Assert.Same(error, Assert.Single(failed.Errors));
    }

    [Fact]
    public async Task CancellationIsObservedBeforeCallingTheChecker()
    {
        var binder = Binder(["n1"]);
        var validation = Validate(Draft(), binder);
        var checker = new RecordingClaimChecker(_ => new ClaimCheckerReply([], null));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Service(checker).CheckAsync(validation, binder, source.Token));
        Assert.Empty(checker.Claims);
    }

    private static ClaimCheckingService Service(RecordingClaimChecker checker) => new(checker);

    private static IReadOnlyList<ClaimVerdict> Supported(IReadOnlyList<CheckerClaim> claims) =>
        claims.Select(claim => new ClaimVerdict(claim.ClaimId, ClaimVerdictKind.Supported, null, [])).ToArray();

    private static RecordingClaimChecker CorrectionChecker(string correctedKind) => new(claims => new ClaimCheckerReply(
        claims.Select(claim => claim.Type == "relationship"
            ? new ClaimVerdict(claim.ClaimId, ClaimVerdictKind.WrongKind, correctedKind, [])
            : new ClaimVerdict(claim.ClaimId, ClaimVerdictKind.Supported, null, [])).ToArray(), null));

    private static DraftValidationOutcome Validate(MentalModelDraft draft, EvidenceBinderModel binder) =>
        new DraftValidationService().Validate(draft, binder, CancellationToken.None);

    private static EvidenceBinderModel Binder(
        IReadOnlyList<string> nodeIds,
        IReadOnlyList<(string Id, string FromNodeId, string ToNodeId)>? edges = null) =>
        DraftValidationFixtureBinderBuilder.Create(nodeIds, edges);

    private static MentalModelDraft Draft(
        IReadOnlyList<DraftParticipant>? participants = null,
        IReadOnlyList<DraftRelationship>? relationships = null,
        IReadOnlyList<BoundStatement>? steps = null,
        IReadOnlyList<BoundStatement>? purposes = null) => new(
        new BoundStatement("Thesis", ["n1"]),
        [new DraftTrack(
            "t1",
            "Track",
            new BoundStatement("Summary", ["n1"]),
            "ParticipantMap",
            participants ?? [Participant("p1", ["n1"]), Participant("p2", ["n2"])],
            relationships ?? [],
            steps ?? [],
            purposes ?? [])],
        []);

    private static DraftParticipant Participant(string id, IReadOnlyList<string> nodes) => new(id, id == "p1" ? "From" : "To", "Role", true, nodes);

    private static DraftRelationship Relationship(
        string id,
        string from,
        string to,
        IReadOnlyList<string> nodes,
        string kind = "depends-on",
        string explanation = "Explanation") => new(id, from, to, kind, explanation, nodes, []);

    private static BinderEvidence BinderEvidence(string nodeId, int startLine, int endLine, string text) => new(
        nodeId,
        ChangeLens.Core.ChangeAnatomy.Models.ChangeAnatomySide.After,
        $"src/{nodeId}.cs",
        startLine,
        endLine,
        true,
        [],
        1,
        text,
        $"sha256:{nodeId}",
        null,
        false,
        false);
}
