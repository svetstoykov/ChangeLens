using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceFrontier.Constants;
using ChangeLens.Core.EvidenceFrontier.Interfaces;
using ChangeLens.Core.EvidenceFrontier.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceFrontierModel = ChangeLens.Core.EvidenceFrontier.Models.EvidenceFrontier;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.EvidenceFrontier.Services;

/// <summary>
///     Builds the bounded evidence omitted from a published reading model.
/// </summary>
/// <remarks>
///     The service is a deterministic, scoped transformation of supplied ranking, graph, policy, and binder results.
///     It does not read repository content and does not need to be thread-safe.
/// </remarks>
/// <param name="options">The frontier bounds. Cannot be <see langword="null" />.</param>
public sealed class EvidenceFrontierService(EvidenceFrontierOptions options) : IEvidenceFrontierService
{
    private readonly EvidenceFrontierOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public EvidenceFrontierModel Build(
        CorrespondenceRanking ranking,
        EvidenceGraphModel graph,
        ContextPolicyOutcome policy,
        EvidenceBinderModel binder,
        IReadOnlySet<string>? usedNodeIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ranking);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this._options.MaximumEntries);
        cancellationToken.ThrowIfCancellationRequested();

        var candidatesByPath = ranking.Candidates
            .GroupBy(candidate => candidate.Path, StringComparer.Ordinal)
            .Select(group => group.OrderBy(candidate => candidate.Rank).ThenBy(candidate => candidate.Path, StringComparer.Ordinal).First())
            .ToDictionary(candidate => candidate.Path, StringComparer.Ordinal);
        var graphPaths = graph.Nodes.Select(node => node.Path).ToHashSet(StringComparer.Ordinal);
        var graphNodesById = ById(graph.Nodes, node => node.NodeId);
        var decisionsByNodeId = ById(policy.Decisions, decision => decision.NodeId);
        var disclosedByNodeId = ById(policy.DisclosedNodes, node => node.NodeId);
        var budgetDroppedNodeIds = binder.Omissions
            .Where(omission => omission.Kind == EvidenceBinderOmissionKind.BudgetDropped
                && omission.Reason != EvidenceBinderLadderStep.Orientation)
            .SelectMany(omission => omission.Items)
            .ToHashSet(StringComparer.Ordinal);
        var boundByNodeId = ById(binder.Evidence, evidence => evidence.NodeId);
        var entries = new List<FrontierEntry>();
        var seenNodeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in candidatesByPath.Values
                     .OrderBy(candidate => candidate.Rank)
                     .ThenBy(candidate => candidate.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (graphPaths.Contains(candidate.Path))
            {
                continue;
            }

            entries.Add(CreateCandidateEntry(candidate));
        }

        foreach (var node in graph.Nodes.OrderByDescending(node => node.Salience).ThenBy(node => node.NodeId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (boundByNodeId.ContainsKey(node.NodeId) || !seenNodeIds.Add(node.NodeId))
            {
                continue;
            }

            candidatesByPath.TryGetValue(node.Path, out var candidate);
            var (kind, reason) = Classify(node.NodeId, decisionsByNodeId, disclosedByNodeId, budgetDroppedNodeIds);
            entries.Add(CreateGraphEntry(node, candidate, kind, reason));
        }

        var postCheck = usedNodeIds is not null;
        var usedBoundNodeCount = 0;
        if (postCheck)
        {
            foreach (var evidence in binder.Evidence.OrderBy(evidence => evidence.Rank).ThenBy(evidence => evidence.NodeId, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (usedNodeIds!.Contains(evidence.NodeId))
                {
                    usedBoundNodeCount++;
                    continue;
                }

                if (!seenNodeIds.Add(evidence.NodeId))
                {
                    continue;
                }

                candidatesByPath.TryGetValue(evidence.Path, out var candidate);
                graphNodesById.TryGetValue(evidence.NodeId, out var graphNode);
                disclosedByNodeId.TryGetValue(evidence.NodeId, out var disclosedNode);
                entries.Add(CreateBoundEntry(evidence, graphNode, disclosedNode, candidate));
            }
        }

        var ordered = entries
            .OrderBy(entry => KindOrder(entry.OmissionKind))
            .ThenBy(entry => entry.CandidateRank ?? int.MaxValue)
            .ThenByDescending(entry => entry.Salience ?? entry.CandidateScore ?? 0)
            .ThenBy(entry => entry.NodeId ?? entry.Path, StringComparer.Ordinal)
            .ToList();
        var byKind = CreateKindCounts(ordered);
        var totalEntryCount = ordered.Count;
        var truncatedEntryCount = Math.Max(0, totalEntryCount - this._options.MaximumEntries);
        if (truncatedEntryCount > 0)
        {
            ordered = ordered.Take(this._options.MaximumEntries).ToList();
        }

        var diagnostics = new FrontierDiagnostics(
            ranking.Candidates.Count,
            graph.Nodes.Count,
            policy.DisclosedNodes.Count,
            binder.Evidence.Count,
            usedBoundNodeCount,
            ordered.Count,
            totalEntryCount,
            truncatedEntryCount,
            byKind[FrontierOmissionKind.CandidateNotQuoted],
            byKind[FrontierOmissionKind.PolicyExcluded],
            byKind[FrontierOmissionKind.BudgetDropped],
            byKind[FrontierOmissionKind.BoundNotUsed],
            postCheck,
            byKind);
        return new EvidenceFrontierModel(ordered, diagnostics);
    }

    private static FrontierEntry CreateCandidateEntry(CorrespondenceCandidate candidate) => new(
        null,
        candidate.Path,
        ChangeAnatomySide.After,
        candidate.Rank,
        candidate.Score,
        null,
        [],
        candidate.DominantSignal,
        candidate.Reasons,
        candidate.OmittedReasonCount,
        FrontierOmissionKind.CandidateNotQuoted,
        "ranked candidate received no evidence node under the graph budget or quote rules",
        candidate.MatchedChangedPaths);

    private static FrontierEntry CreateGraphEntry(
        EvidenceNode node,
        CorrespondenceCandidate? candidate,
        string omissionKind,
        string omissionReason) => new(
        node.NodeId,
        node.Path,
        node.Side,
        candidate?.Rank,
        candidate?.Score,
        node.Salience,
        node.Origins,
        candidate?.DominantSignal,
        candidate?.Reasons ?? [],
        candidate?.OmittedReasonCount ?? 0,
        omissionKind,
        omissionReason,
        candidate?.MatchedChangedPaths ?? []);

    private static FrontierEntry CreateBoundEntry(
        BinderEvidence evidence,
        EvidenceNode? graphNode,
        DisclosedEvidenceNode? disclosedNode,
        CorrespondenceCandidate? candidate) => new(
        evidence.NodeId,
        evidence.Path,
        graphNode?.Side ?? evidence.Side,
        candidate?.Rank,
        candidate?.Score,
        disclosedNode?.Salience ?? graphNode?.Salience,
        graphNode?.Origins ?? evidence.Origins,
        candidate?.DominantSignal,
        candidate?.Reasons ?? [],
        candidate?.OmittedReasonCount ?? 0,
        FrontierOmissionKind.BoundNotUsed,
        "disclosed and bound but not cited by the verified mental model",
        candidate?.MatchedChangedPaths ?? []);

    /// <summary>
    ///     Classifies an unbound graph node using policy first, then binder budget evidence.
    /// </summary>
    /// <param name="nodeId">The graph node id.</param>
    /// <param name="decisions">The first policy decision for each node id.</param>
    /// <param name="disclosed">The first disclosed node for each node id.</param>
    /// <param name="budgetDroppedNodeIds">The node ids removed by non-orientation budget steps.</param>
    /// <returns>The stable omission kind and explanation.</returns>
    private static (string Kind, string Reason) Classify(
        string nodeId,
        IReadOnlyDictionary<string, ContextPolicyDecision> decisions,
        IReadOnlyDictionary<string, DisclosedEvidenceNode> disclosed,
        IReadOnlySet<string> budgetDroppedNodeIds)
    {
        decisions.TryGetValue(nodeId, out var decision);
        if (decision?.Verdict == ContextPolicyVerdict.Exclude)
        {
            return (FrontierOmissionKind.PolicyExcluded, decision.ExclusionReason ?? "excluded");
        }

        if (budgetDroppedNodeIds.Contains(nodeId))
        {
            return (FrontierOmissionKind.BudgetDropped, "dropped by the binder character budget ladder");
        }

        if (disclosed.ContainsKey(nodeId))
        {
            return (FrontierOmissionKind.BudgetDropped, "disclosed by policy but not admitted to the binder");
        }

        if (decision is not null)
        {
            return (FrontierOmissionKind.PolicyExcluded, $"policy returned {decision.Verdict} but disclosed no node for it");
        }

        return (FrontierOmissionKind.CandidateNotQuoted, "graph node was not disclosed and has no policy decision");
    }

    private static Dictionary<string, T> ById<T>(IEnumerable<T> items, Func<T, string> id) => items
        .GroupBy(id, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    /// <summary>
    ///     Counts every omission kind before the returned-entry cap is applied.
    /// </summary>
    /// <param name="entries">The complete ordered entry set.</param>
    /// <returns>Counts for every stable omission kind.</returns>
    private static Dictionary<string, int> CreateKindCounts(IEnumerable<FrontierEntry> entries)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [FrontierOmissionKind.CandidateNotQuoted] = 0,
            [FrontierOmissionKind.PolicyExcluded] = 0,
            [FrontierOmissionKind.BudgetDropped] = 0,
            [FrontierOmissionKind.BoundNotUsed] = 0,
        };
        foreach (var group in entries.GroupBy(entry => entry.OmissionKind, StringComparer.Ordinal))
        {
            counts[group.Key] = group.Count();
        }

        return counts;
    }

    private static int KindOrder(string kind) => kind switch
    {
        FrontierOmissionKind.BoundNotUsed => 0,
        FrontierOmissionKind.BudgetDropped => 1,
        FrontierOmissionKind.PolicyExcluded => 2,
        FrontierOmissionKind.CandidateNotQuoted => 3,
        _ => 4,
    };
}
