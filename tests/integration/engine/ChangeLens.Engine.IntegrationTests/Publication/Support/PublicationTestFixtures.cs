using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ClaimChecking.Interfaces;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceFrontier.Interfaces;
using ChangeLens.Core.EvidenceFrontier.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.IntegrationTests.DraftValidation.Support;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceFrontierModel = ChangeLens.Core.EvidenceFrontier.Models.EvidenceFrontier;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Engine.IntegrationTests.Publication.Support;

internal static class PublicationTestFixtures
{
    internal static EvidenceBinderModel Binder(params string[] nodeIds) => DraftValidationFixtureBinderBuilder.Create(nodeIds);

    internal static EvidenceGraphModel Graph(params string[] nodeIds)
    {
        var nodes = nodeIds.Select(nodeId => new EvidenceNode(
            nodeId, $"src/{nodeId}.cs", ChangeAnatomySide.After, $"blob-{nodeId}", 1, 1, $"quote for {nodeId}",
            $"sha256:{nodeId}", true, [], 1, false)).ToArray();
        return new EvidenceGraphModel(nodes, [], new EvidenceGraphDiagnostics(
            nodes.Length, 0, 0, nodes.Length, 0, nodes.Length, nodes.Length, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>()));
    }

    internal static ContextPolicyOutcome Policy() => new(
        [], [], [], [], new ContextPolicyDiagnostics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(), new Dictionary<string, int>()));

    internal static CorrespondenceRanking Ranking() => new(
        [], new CorrespondenceDiagnostics(0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>()));

    internal static DraftValidationOutcome Validation(params string[] nodeIds)
    {
        var thesis = new BoundStatement("Thesis", nodeIds);
        var summary = new BoundStatement("Summary", nodeIds);
        var track = new DraftTrack("track", "Track", summary, "Walk", [], [], [], []);
        return new DraftValidationOutcome(new MentalModelDraft(thesis, [track], []), [], 0, 0, 0, 0);
    }

    internal static ClaimCheckingSummary Summary() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, [], [], []);
}

internal sealed class RecordingClaimCheckingService(ClaimCheckingOutcome outcome) : IClaimCheckingService
{
    internal bool Called { get; private set; }

    public Task<Result<ClaimCheckingOutcome>> CheckAsync(
        DraftValidationOutcome validation,
        EvidenceBinderModel binder,
        CancellationToken cancellationToken)
    {
        this.Called = true;
        return Task.FromResult(Result.Success(outcome));
    }
}

internal sealed class RecordingFrontierService : IEvidenceFrontierService
{
    internal IReadOnlySet<string>? UsedNodeIds { get; private set; }

    public EvidenceFrontierModel Build(
        CorrespondenceRanking ranking,
        EvidenceGraphModel graph,
        ContextPolicyOutcome policy,
        EvidenceBinderModel binder,
        IReadOnlySet<string>? usedNodeIds,
        CancellationToken cancellationToken)
    {
        this.UsedNodeIds = usedNodeIds;
        var entries = usedNodeIds is null
            ? []
            : binder.Evidence
                .Where(evidence => !usedNodeIds.Contains(evidence.NodeId))
                .Select(evidence => new FrontierEntry(evidence.NodeId, evidence.Path, evidence.Side, null, null, null,
                    evidence.Origins, null, [], 0, "boundNotUsed", "unused", []))
                .ToArray();
        return new EvidenceFrontierModel(entries, new FrontierDiagnostics(
            ranking.Candidates.Count, graph.Nodes.Count, policy.DisclosedNodes.Count, binder.Evidence.Count,
            usedNodeIds?.Count ?? 0, entries.Length, entries.Length, 0, 0, 0, 0, entries.Length,
            usedNodeIds is not null, new Dictionary<string, int>()));
    }
}

internal sealed class RecordingClaimChecker : IClaimChecker
{
    public Task<Result<ClaimCheckerReply>> CheckAsync(IReadOnlyList<CheckerClaim> claims, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(new ClaimCheckerReply([], null)));
}
