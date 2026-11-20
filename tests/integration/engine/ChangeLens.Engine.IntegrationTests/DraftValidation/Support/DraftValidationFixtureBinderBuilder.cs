using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.Snapshots.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Engine.IntegrationTests.DraftValidation.Support;

/// <summary>
///     Builds small evidence binders for deterministic draft-validation integration tests.
/// </summary>
internal static class DraftValidationFixtureBinderBuilder
{
    /// <summary>
    ///     Creates a binder with the supplied disclosed nodes, match edges, and curator limits.
    /// </summary>
    /// <param name="nodeIds">The evidence node identifiers to disclose.</param>
    /// <param name="edges">The undirected match edges to disclose.</param>
    /// <param name="limits">The curator limits, or <see langword="null" /> for test defaults.</param>
    /// <returns>A binder containing only controlled fixture data.</returns>
    internal static EvidenceBinderModel Create(
        IReadOnlyList<string> nodeIds,
        IReadOnlyList<(string Id, string FromNodeId, string ToNodeId)>? edges = null,
        CuratorLimits? limits = null)
    {
        var evidence = nodeIds.Select((nodeId, index) => new BinderEvidence(
            nodeId,
            ChangeAnatomySide.After,
            $"src/{nodeId}.cs",
            1,
            1,
            true,
            [],
            index + 1,
            $"quote for {nodeId}",
            $"sha256:{nodeId}",
            null,
            false,
            false)).ToArray();
        var matchEdges = (edges ?? []).Select(edge => new BinderMatchEdge(
            edge.Id,
            edge.FromNodeId,
            edge.ToNodeId,
            MatchEdgeKind.SharedIdentifier,
            "fixture",
            MatchEdgeAnchor.Quoted,
            MatchEdgeAnchor.Quoted,
            1)).ToArray();
        var contractLimits = limits ?? new CuratorLimits(8, 8, 8, 8, 200, CuratorContractConstants.IdFormat);
        var contract = new BinderContract(
            CuratorContractConstants.RelationshipKinds,
            CuratorContractConstants.TrackShapes,
            contractLimits);
        return new EvidenceBinderModel(
            new BinderComparison(
                Guid.Empty,
                "fixture",
                "target",
                "target-revision",
                "head-revision",
                "merge-base",
                0,
                new ExcludedUncommittedCounts(0, 0, 0, 0, 0)),
            null,
            [],
            evidence,
            matchEdges,
            new BinderOrientation([]),
            contract,
            [],
            new BinderDiagnostics(0, 0, 0, 0, false, [], evidence.Length, 0, 0, matchEdges.Length, 0, 0, 0, []));
    }
}
