using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceFrontier.Constants;
using ChangeLens.Core.EvidenceFrontier.Models;
using ChangeLens.Core.EvidenceFrontier.Services;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.Snapshots.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.EvidenceFrontier;

public sealed class EvidenceFrontierServiceTests
{
    [Fact]
    public void BuildsEachOmissionKindAndPreservesCandidateMetadata()
    {
        var signal = new CoChangeCorrespondenceSignal("src/changed.cs", 2.5, 3);
        var candidate = Candidate("src/missing.cs", 1, 9.25, signal);
        var graphNode = Node("n001", "src/policy.cs", 9);
        var budgetNode = Node("n002", "src/budget.cs", 8);
        var orientationNode = Node("n003", "src/orientation.cs", 7);
        var noDecisionNode = Node("n004", "src/no-decision.cs", 6);
        var ranking = Ranking(candidate);
        var graph = Graph(graphNode, budgetNode, orientationNode, noDecisionNode);
        var policy = Policy(
            Decision(graphNode, ContextPolicyVerdict.Exclude, "secret"),
            Decision(budgetNode, ContextPolicyVerdict.Allow, null),
            budgetNode);
        var binder = Binder(
            [],
            new BinderOmission(EvidenceBinderOmissionKind.BudgetDropped, 1, EvidenceBinderLadderStep.CandidateNodeSalience, [budgetNode.NodeId]),
            new BinderOmission(EvidenceBinderOmissionKind.BudgetDropped, 1, EvidenceBinderLadderStep.ChangedFileNodeSalience, [graphNode.NodeId]),
            new BinderOmission(EvidenceBinderOmissionKind.BudgetDropped, 1, EvidenceBinderLadderStep.Orientation, ["src"]));

        var frontier = new EvidenceFrontierService(new EvidenceFrontierOptions()).Build(
            ranking, graph, policy, binder, null, CancellationToken.None);

        Assert.Equal(5, frontier.Diagnostics.TotalEntryCount);
        Assert.Equal(3, frontier.Diagnostics.CandidateNotQuotedCount);
        Assert.Equal(1, frontier.Diagnostics.PolicyExcludedCount);
        Assert.Equal(1, frontier.Diagnostics.BudgetDroppedCount);
        Assert.Equal(0, frontier.Diagnostics.BoundNotUsedCount);
        Assert.Equal(
            [FrontierOmissionKind.BudgetDropped, FrontierOmissionKind.PolicyExcluded, FrontierOmissionKind.CandidateNotQuoted,
                FrontierOmissionKind.CandidateNotQuoted, FrontierOmissionKind.CandidateNotQuoted],
            frontier.Entries.Select(entry => entry.OmissionKind));

        var candidateEntry = Assert.Single(frontier.Entries, entry => entry.Path == candidate.Path);
        Assert.Equal(candidate.Rank, candidateEntry.CandidateRank);
        Assert.Equal(candidate.Score, candidateEntry.CandidateScore);
        Assert.Equal(candidate.DominantSignal, candidateEntry.DominantSignal);
        Assert.Same(signal, Assert.Single(candidateEntry.Reasons));
        Assert.Equal(candidate.OmittedReasonCount, candidateEntry.OmittedReasonCount);
        Assert.Equal(candidate.MatchedChangedPaths, candidateEntry.MatchedChangedPaths);

        var orientationEntry = Assert.Single(frontier.Entries, entry => entry.NodeId == orientationNode.NodeId);
        Assert.Equal(FrontierOmissionKind.CandidateNotQuoted, orientationEntry.OmissionKind);
        Assert.Equal(FrontierOmissionKind.PolicyExcluded, frontier.Entries.Single(entry => entry.NodeId == graphNode.NodeId).OmissionKind);
    }

    [Fact]
    public void OrdersEntriesAndReportsCountsBeforeTheCap()
    {
        var graphNodes = Enumerable.Range(1, 5).Select(index => Node($"n{index:000}", $"src/{index}.cs", 10 - index)).ToArray();
        var graph = Graph(graphNodes);
        var policy = Policy(graphNodes.Select(node => Decision(node, ContextPolicyVerdict.Exclude, "excluded")).ToArray());
        var binder = Binder(
            [],
            new BinderOmission(EvidenceBinderOmissionKind.PolicyExcluded, 5, "excluded", graphNodes.Select(node => node.NodeId).ToArray()));
        var options = new EvidenceFrontierOptions { MaximumEntries = 2 };

        var frontier = new EvidenceFrontierService(options).Build(
            Ranking(), graph, policy, binder, null, CancellationToken.None);

        Assert.Equal(5, frontier.Diagnostics.TotalEntryCount);
        Assert.Equal(2, frontier.Diagnostics.EntryCount);
        Assert.Equal(3, frontier.Diagnostics.TruncatedEntryCount);
        Assert.Equal(5, frontier.Diagnostics.PolicyExcludedCount);
        Assert.Equal(["n001", "n002"], frontier.Entries.Select(entry => entry.NodeId));
    }

    [Fact]
    public void BoundNotUsedRequiresAnExplicitUsedNodeSet()
    {
        var node = Node("n001", "src/bound.cs", 4);
        var binder = Binder([Evidence(node)]);
        var service = new EvidenceFrontierService(new EvidenceFrontierOptions());

        var checkingOff = service.Build(
            Ranking(), Graph(node), Policy(Decision(node, ContextPolicyVerdict.Allow, null), node), binder, null, CancellationToken.None);
        var checkedWithNoCitations = service.Build(
            Ranking(), Graph(node), Policy(Decision(node, ContextPolicyVerdict.Allow, null), node), binder,
            new HashSet<string>(StringComparer.Ordinal), CancellationToken.None);

        Assert.False(checkingOff.Diagnostics.PostCheck);
        Assert.DoesNotContain(checkingOff.Entries, entry => entry.OmissionKind == FrontierOmissionKind.BoundNotUsed);
        Assert.True(checkedWithNoCitations.Diagnostics.PostCheck);
        Assert.Equal(0, checkedWithNoCitations.Diagnostics.UsedNodeCount);
        Assert.Equal(1, checkedWithNoCitations.Diagnostics.BoundNotUsedCount);
        Assert.Equal(FrontierOmissionKind.BoundNotUsed, Assert.Single(checkedWithNoCitations.Entries).OmissionKind);
    }

    [Fact]
    public void CancellationIsObservedBeforeBuilding()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => new EvidenceFrontierService(new EvidenceFrontierOptions()).Build(
            Ranking(), Graph(), Policy(), Binder([]), null, cancellation.Token));
    }

    private static CorrespondenceCandidate Candidate(string path, int rank, double score, CorrespondenceSignal signal) => new(
        rank,
        path,
        $"blob-{rank}",
        score,
        signal.Kind,
        ["src/changed.cs"],
        [signal, new CoChangeCorrespondenceSignal("src/other.cs", 1, 1)],
        [signal]);

    private static CorrespondenceRanking Ranking(params CorrespondenceCandidate[] candidates) => new(
        candidates,
        new CorrespondenceDiagnostics(0, false, 0, 0, 0, 0, 0, 0, 0, candidates.Length, 0, candidates.Length, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>()));

    private static EvidenceGraph Graph(params EvidenceNode[] nodes) => new(
        nodes,
        [],
        new EvidenceGraphDiagnostics(nodes.Length, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>()));

    private static ContextPolicyOutcome Policy(params object[] items)
    {
        var decisions = items.OfType<ContextPolicyDecision>().ToArray();
        var disclosed = items.OfType<DisclosedEvidenceNode>().Concat(items.OfType<EvidenceNode>().Select(Disclosed)).ToArray();
        return new ContextPolicyOutcome(
            decisions,
            [],
            disclosed,
            [],
            new ContextPolicyDiagnostics(decisions.Length, disclosed.Length, 0, decisions.Length - disclosed.Length, 0, 0, 0, 0, 0, 0,
                new Dictionary<string, int>(), new Dictionary<string, int>()));
    }

    private static ContextPolicyDecision Decision(EvidenceNode node, ContextPolicyVerdict verdict, string? reason) => new(
        node.NodeId,
        node.Path,
        verdict,
        reason,
        []);

    private static DisclosedEvidenceNode Disclosed(EvidenceNode node) => new(
        node.NodeId,
        node.Path,
        node.Side,
        node.ObjectId,
        node.StartLine,
        node.EndLine,
        node.IsChangedFile,
        node.Origins,
        node.Salience,
        node.QuotedText,
        node.ContentHash,
        null,
        node.Truncated);

    private static EvidenceBinderModel Binder(
        IReadOnlyList<BinderEvidence> evidence,
        params BinderOmission[] omissions) => new(
        new BinderComparison(Guid.Empty, "repo", "target", "target", "head", "base", 0, new ExcludedUncommittedCounts(0, 0, 0, 0, 0)),
        null,
        [],
        evidence,
        [],
        new BinderOrientation([]),
        new BinderContract([], [], new CuratorLimits(1, 1, 1, 1, 1, "id")),
        omissions,
        new BinderDiagnostics(0, 0, 0, 0, false, [], evidence.Count, 0, 0, 0, 0, 0, 0, []));

    private static BinderEvidence Evidence(EvidenceNode node) => new(
        node.NodeId,
        node.Side,
        node.Path,
        node.StartLine,
        node.EndLine,
        node.IsChangedFile,
        node.Origins,
        1,
        node.QuotedText,
        node.ContentHash,
        null,
        false,
        node.Truncated);

    private static EvidenceNode Node(string nodeId, string path, double salience) => new(
        nodeId,
        path,
        ChangeAnatomySide.After,
        $"object-{nodeId}",
        1,
        2,
        "line one\nline two",
        $"hash-{nodeId}",
        false,
        [EvidenceNodeOrigin.MatchWindow],
        salience,
        false);
}
