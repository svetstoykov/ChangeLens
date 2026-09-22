using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.EvidenceFrontier.Constants;
using ChangeLens.Core.EvidenceFrontier.Interfaces;
using ChangeLens.Core.EvidenceFrontier.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceFrontierModel = ChangeLens.Core.EvidenceFrontier.Models.EvidenceFrontier;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Engine.IntegrationTests.Publication.Support;

/// <summary>
///     Records the used-node set and emits bound-not-used entries for unused binder nodes.
/// </summary>
internal sealed class RecordingFrontierService : IEvidenceFrontierService
{
    /// <summary>
    ///     Gets the used-node set supplied to the last <see cref="Build" /> call.
    /// </summary>
    internal IReadOnlySet<string>? UsedNodeIds { get; private set; }

    /// <inheritdoc />
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
                    evidence.Origins, null, [], 0, FrontierOmissionKind.BoundNotUsed, "unused", []))
                .ToArray();
        return new EvidenceFrontierModel(entries, new FrontierDiagnostics(
            ranking.Candidates.Count, graph.Nodes.Count, policy.DisclosedNodes.Count, binder.Evidence.Count,
            usedNodeIds?.Count ?? 0, entries.Length, entries.Length, 0, 0, 0, 0, entries.Length,
            usedNodeIds is not null, new Dictionary<string, int>()));
    }
}
