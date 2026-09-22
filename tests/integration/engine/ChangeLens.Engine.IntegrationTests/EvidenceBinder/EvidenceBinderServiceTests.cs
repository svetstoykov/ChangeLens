using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceBinder.Services;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.Snapshots.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Engine.IntegrationTests.EvidenceBinder;

/// <summary>
///     Verifies evidence binder assembly against controlled frozen analysis fixtures.
/// </summary>
public sealed class EvidenceBinderServiceTests
{
    [Fact]
    public void BinderIncludesRenameProvenanceAndOmitsEdgesAndExcludedContentFromPayload()
    {
        var before = Node("before", "legacy/name.cs", ChangeAnatomySide.Before, false, EvidenceNodeOrigin.ChangedHunk, "before quote");
        var after = Node("after", "new/name.cs", ChangeAnatomySide.After, true, EvidenceNodeOrigin.ChangedHunk, "after quote");
        var candidate = Node("candidate", "src/consumer.cs", ChangeAnatomySide.After, false, EvidenceNodeOrigin.MatchWindow, "candidate quote");
        var excluded = Node("excluded", "src/secret.cs", ChangeAnatomySide.After, false, EvidenceNodeOrigin.MatchWindow, "excluded secret text");
        var edge = new DisclosedMatchEdge(
            "edge-1", "after", "candidate", MatchEdgeKind.SharedIdentifier, "OrderService", MatchEdgeAnchor.Quoted,
            MatchEdgeAnchor.Quoted, 1.2);
        var policy = Policy(
            [before, after, candidate],
            [
                Decision(before), Decision(after), Decision(candidate),
                new ContextPolicyDecision("excluded", excluded.Path, ContextPolicyVerdict.Exclude, "secret", []),
            ],
            [edge]);
        var request = Request(
            policy,
            new SnapshotManifestEntry("new/name.cs", "legacy/name.cs", SnapshotChangeCategory.Renamed, "100644", "100755", Blob('a'), Blob('b')),
            ["legacy/name.cs", "new/name.cs", "src/consumer.cs", excluded.Path, ".env", "credentials.json", "node_modules/package.js"],
            "developer context that is deliberately longer than the configured cap");

        var result = Service(new EvidenceBinderOptions { MaximumDeveloperContextCharacters = 12 }).Assemble(
            request with { }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var binder = Assert.IsType<EvidenceBinderModel>(result.Data);
        var changedFile = Assert.Single(binder.ChangedFiles);
        Assert.Equal("new/name.cs", changedFile.Path);
        Assert.Equal("legacy/name.cs", changedFile.OriginalPath);
        Assert.Equal("100644", changedFile.BeforeMode);
        Assert.Equal("100755", changedFile.AfterMode);
        Assert.Contains("before", changedFile.EvidenceNodeIds);
        Assert.Contains("after", changedFile.EvidenceNodeIds);
        Assert.DoesNotContain(binder.Evidence, node => node.NodeId == "excluded");
        Assert.Single(binder.MatchEdges);
        Assert.Contains(
            binder.Omissions,
            omission => omission.Kind == EvidenceBinderOmissionKind.PolicyExcluded && omission.Items.Contains("excluded"));
        Assert.Contains(binder.Omissions, omission => omission.Kind == EvidenceBinderOmissionKind.DeveloperContextTruncated);
        Assert.DoesNotContain(binder.Orientation.Directories.SelectMany(directory => directory.Paths), path => path == excluded.Path);

        var json = EvidenceBinderJson.SerializePayload(binder);
        Assert.DoesNotContain("matchEdges", json, StringComparison.Ordinal);
        Assert.DoesNotContain("edge-1", json);
        Assert.DoesNotContain("OrderService", json);
        Assert.DoesNotContain("excluded secret text", json);
        Assert.DoesNotContain("anatomy", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("developer-supplied hint, not evidence", json);
        Assert.DoesNotContain(binder.Orientation.Directories.SelectMany(directory => directory.Paths), path => path == ".env");
        Assert.DoesNotContain(binder.Orientation.Directories.SelectMany(directory => directory.Paths), path => path == "credentials.json");
        Assert.DoesNotContain(binder.Orientation.Directories.SelectMany(directory => directory.Paths), path => path == "node_modules/package.js");
        var objectJson = JsonSerializer.Serialize(binder);
        Assert.DoesNotContain("edge-1", objectJson, StringComparison.Ordinal);
    }

    [Fact]
    public void PolicyInvariantDropsAreDiagnosedSeparatelyFromPolicyExclusions()
    {
        var duplicate = Node("duplicate", "changed.cs", ChangeAnatomySide.After, true, EvidenceNodeOrigin.ChangedHunk, "duplicate");
        var missing = Node("missing", "changed.cs", ChangeAnatomySide.After, true, EvidenceNodeOrigin.ChangedHunk, "missing");
        var request = Request(
            Policy(
                [duplicate, duplicate, missing],
                [Decision(duplicate)],
                []),
            new SnapshotManifestEntry("changed.cs", null, SnapshotChangeCategory.Modified, "100644", "100644", Blob('a'), Blob('b')),
            ["changed.cs"],
            null);

        var result = Service(new EvidenceBinderOptions()).Assemble(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var binder = Assert.IsType<EvidenceBinderModel>(result.Data);
        Assert.Empty(binder.Evidence);
        Assert.Equal(["duplicate", "missing"], binder.Diagnostics.PolicyInvariantViolationNodeIds);
        Assert.All(binder.Omissions.Where(omission => omission.Kind == EvidenceBinderOmissionKind.PolicyInvariantViolation), omission =>
            Assert.Equal("policyInvariantViolated", omission.Reason));
    }

    [Fact]
    public void MalformedDecisionPathAndRedactedHashAreDroppedAsPolicyInvariantViolations()
    {
        var wrongPath = Node("wrong-path", "changed.cs", ChangeAnatomySide.After, true, EvidenceNodeOrigin.ChangedHunk, "quote");
        var wrongHash = new DisclosedEvidenceNode(
            "wrong-hash", "changed.cs", ChangeAnatomySide.After, Blob('f'), 1, 1, true, [EvidenceNodeOrigin.ChangedHunk], 1,
            "redacted quote", "sha256:content", "sha256:not-the-text", false);
        var request = Request(
            Policy(
                [wrongPath, wrongHash],
                [
                    new ContextPolicyDecision("wrong-path", "other.cs", ContextPolicyVerdict.Allow, null, []),
                    new ContextPolicyDecision("wrong-hash", "changed.cs", ContextPolicyVerdict.Redact, null, []),
                ],
                []),
            new SnapshotManifestEntry("changed.cs", null, SnapshotChangeCategory.Modified, "100644", "100644", Blob('a'), Blob('b')),
            ["changed.cs"],
            null);

        var result = Service(new EvidenceBinderOptions()).Assemble(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var binder = Assert.IsType<EvidenceBinderModel>(result.Data);
        Assert.Empty(binder.Evidence);
        Assert.Equal(["wrong-hash", "wrong-path"], binder.Diagnostics.PolicyInvariantViolationNodeIds);
    }

    [Fact]
    public void BudgetLadderDropsInDefinedOrderAndRecordsNodeIds()
    {
        var changed1 = Node("changed-1", "changed.cs", ChangeAnatomySide.After, true, EvidenceNodeOrigin.ChangedHunk, new string('c', 5_000), 0.1);
        var changed2 = Node("changed-2", "changed.cs", ChangeAnatomySide.After, true, EvidenceNodeOrigin.ChangedHunk, new string('d', 5_000), 0.2);
        var candidateHead = Node(
            "head", "candidate.cs", ChangeAnatomySide.After, false, EvidenceNodeOrigin.CandidateHead, new string('h', 5_000), 0.01);
        var candidate = Node(
            "candidate", "candidate.cs", ChangeAnatomySide.After, false, EvidenceNodeOrigin.MatchWindow, new string('q', 5_000), 0.02);
        var request = Request(
            Policy(
                [changed1, changed2, candidateHead, candidate],
                [Decision(changed1), Decision(changed2), Decision(candidateHead), Decision(candidate)],
                []),
            new SnapshotManifestEntry("changed.cs", null, SnapshotChangeCategory.Modified, "100644", "100644", Blob('a'), Blob('b')),
            ["changed.cs", "candidate.cs", "candidate-long-neighbour.cs"],
            null);

        var result = Service(new EvidenceBinderOptions { MaximumBinderCharacters = 6_000, TargetUtilization = 1 }).Assemble(
            request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var binder = Assert.IsType<EvidenceBinderModel>(result.Data);
        var steps = binder.Diagnostics.LadderStepsApplied;
        var orderedSteps = steps.ToList();
        Assert.True(
            orderedSteps.IndexOf(EvidenceBinderLadderStep.CandidateHeadNode)
            < orderedSteps.IndexOf(EvidenceBinderLadderStep.CandidateNodeSalience));
        Assert.True(
            orderedSteps.IndexOf(EvidenceBinderLadderStep.CandidateNodeSalience)
            < orderedSteps.IndexOf(EvidenceBinderLadderStep.Orientation));
        Assert.True(
            orderedSteps.IndexOf(EvidenceBinderLadderStep.Orientation)
            < orderedSteps.IndexOf(EvidenceBinderLadderStep.ChangedFileNodeSalience));
        Assert.Contains(binder.Omissions, omission => omission.Kind == EvidenceBinderOmissionKind.BudgetDropped && omission.Items.Contains("head"));
        Assert.Contains(
            binder.Omissions,
            omission => omission.Kind == EvidenceBinderOmissionKind.BudgetDropped && omission.Items.Contains("candidate"));
        Assert.DoesNotContain(binder.Evidence, node => node.NodeId is "head" or "candidate");
        var changedFile = Assert.Single(binder.ChangedFiles);
        Assert.All(changedFile.EvidenceNodeIds, nodeId => Assert.Contains(binder.Evidence, node => node.NodeId == nodeId));
    }

    [Fact]
    public void OrientationKeepsDirectoriesThatSurviveARenameOutOfAnAbsentDirectory()
    {
        var before = Node("before", "legacy/name.cs", ChangeAnatomySide.Before, false, EvidenceNodeOrigin.ChangedHunk, "before quote");
        var after = Node("after", "new/name.cs", ChangeAnatomySide.After, true, EvidenceNodeOrigin.ChangedHunk, "after quote");
        var candidate = Node("candidate", "src/consumer.cs", ChangeAnatomySide.After, false, EvidenceNodeOrigin.MatchWindow, "candidate quote");
        var request = Request(
            Policy([before, after, candidate], [Decision(before), Decision(after), Decision(candidate)], []),
            new SnapshotManifestEntry("new/name.cs", "legacy/name.cs", SnapshotChangeCategory.Renamed, "100644", "100644", Blob('a'), Blob('b')),
            ["new/name.cs", "src/consumer.cs"],
            null);

        var result = Service(new EvidenceBinderOptions()).Assemble(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var binder = Assert.IsType<EvidenceBinderModel>(result.Data);
        Assert.Equal(["new", "src"], binder.Orientation.Directories.Select(directory => directory.Path));
        Assert.Equal(2, binder.Diagnostics.OrientationPathCount);
    }

    [Fact]
    public void OrientationKeepsDirectoriesThatSurviveADeletionEmptyingAnEarlierDirectory()
    {
        var deleted = Node("deleted", "archive/gone.cs", ChangeAnatomySide.Before, true, EvidenceNodeOrigin.ChangedHunk, "gone quote");
        var candidate = Node("candidate", "src/consumer.cs", ChangeAnatomySide.After, false, EvidenceNodeOrigin.MatchWindow, "candidate quote");
        var request = Request(
            Policy([deleted, candidate], [Decision(deleted), Decision(candidate)], []),
            new SnapshotManifestEntry("archive/gone.cs", null, SnapshotChangeCategory.Deleted, "100644", "100644", Blob('a'), Blob('b')),
            ["src/consumer.cs"],
            null);

        var result = Service(new EvidenceBinderOptions()).Assemble(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var binder = Assert.IsType<EvidenceBinderModel>(result.Data);
        var directory = Assert.Single(binder.Orientation.Directories);
        Assert.Equal("src", directory.Path);
        Assert.Equal(["src/consumer.cs"], directory.Paths);
        Assert.Equal(1, binder.Diagnostics.OrientationPathCount);
    }

    [Fact]
    public void IrreducibleMandatoryContentFailsTheHardBudget()
    {
        var node = Node("changed", "changed.cs", ChangeAnatomySide.After, true, EvidenceNodeOrigin.ChangedHunk, "quote");
        var request = Request(
            Policy([node], [Decision(node)], []),
            new SnapshotManifestEntry("changed.cs", null, SnapshotChangeCategory.Modified, "100644", "100644", Blob('a'), Blob('b')),
            ["changed.cs"],
            null);

        var result = Service(new EvidenceBinderOptions { MaximumBinderCharacters = 1, TargetUtilization = 1 }).Assemble(
            request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EvidenceBinderErrorCode.BudgetExceeded, Assert.Single(result.Errors).Code);
    }

    private static EvidenceBinderService Service(EvidenceBinderOptions options) => new(options, NullLogger<EvidenceBinderService>.Instance);

    private static ContextPolicyDecision Decision(DisclosedEvidenceNode node) => new(
        node.NodeId, node.Path, node.IsRedacted ? ContextPolicyVerdict.Redact : ContextPolicyVerdict.Allow, null, []);

    private static DisclosedEvidenceNode Node(
        string id,
        string path,
        ChangeAnatomySide side,
        bool changed,
        EvidenceNodeOrigin origin,
        string text,
        double salience = 1) => new(id, path, side, Blob('f'), 1, 1, changed, [origin], salience, text, "sha256:content", null, false);

    private static EvidenceBinderRequest Request(
        ContextPolicyOutcome policy,
        SnapshotManifestEntry entry,
        IReadOnlyList<string> treePaths,
        string? developerContext) => new(
            new AnalysisRunDetail(
                Guid.NewGuid(),
                AnalysisRunState.Collecting,
                new AnalysisRepositoryIdentity(Guid.NewGuid(), "fixture", "/fixture", "fixture", Blob('h')),
                new AnalysisComparisonIdentity("target", Blob('t'), "fresh"),
                null,
                1,
                null,
                42,
                Guid.NewGuid(),
                "manifest",
                1,
                new ExcludedUncommittedCounts(1, 1, 0, 0, 0),
                null,
                null,
                null,
                false,
                null,
                null,
                null),
            new SnapshotCapture(
                new SnapshotManifest(Guid.NewGuid(), "manifest", "fixture", "target", Blob('t'), Blob('h'), Blob('m'), [entry]),
                new ExcludedUncommittedCounts(1, 1, 0, 0, 0)),
            policy,
            new FrozenGitTreeListing(treePaths.Select(path => new FrozenGitTreeFile(path, Blob('z'), 10, "100644")).ToList(), false),
            new ChangeAnatomy(
                [new ChangedFileAnatomy(entry.Path, entry.OriginalPath, entry.Category, [], false, null)],
                new ChangeAnatomyDiagnostics(1, 1, 0, 0, 0, 0, 0, new Dictionary<string, int>(), new Dictionary<string, int>())),
            developerContext);

    private static ContextPolicyOutcome Policy(
        IReadOnlyList<DisclosedEvidenceNode> nodes,
        IReadOnlyList<ContextPolicyDecision> decisions,
        IReadOnlyList<DisclosedMatchEdge> edges) => new(
        decisions,
        [],
        nodes,
        edges,
        new ContextPolicyDiagnostics(nodes.Count, nodes.Count, 0, 0, 0, nodes.Sum(node => node.Text.Length), edges.Count, edges.Count, 0, 0,
            new Dictionary<string, int>(), new Dictionary<string, int>()));

    private static string Blob(char value) => new(value, 40);
}
