using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.MentalModels.Helpers;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Publication.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.Publication.Helpers;

/// <summary>Builds claim-addressed citations from a published mental model.</summary>
public static class CitationBuilder
{
    /// <summary>Builds one citation for each held node cited by each published claim.</summary>
    /// <param name="model">The published mental model.</param>
    /// <param name="binder">The evidence binder.</param>
    /// <param name="graph">The evidence graph.</param>
    /// <param name="facts">The checker facts.</param>
    /// <returns>The citations in published claim order and binder evidence order.</returns>
    public static IReadOnlyList<Citation> Build(MentalModel model, EvidenceBinderModel binder, EvidenceGraphModel graph, CheckerFacts facts)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(graph);
        var graphById = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var evidenceById = binder.Evidence.ToDictionary(evidence => evidence.NodeId, StringComparer.Ordinal);
        var citations = new List<Citation>();
        foreach (var claimant in EnumerateClaimants(model))
        {
            var provenance = claimant.IsParticipant
                ? CitationProvenance.Unchecked
                : facts.Ran
                    ? claimant.IsThesis ? CitationProvenance.Derived : CitationProvenance.Checked
                    : CitationProvenance.Unchecked;
            foreach (var nodeId in claimant.NodeIds.Distinct(StringComparer.Ordinal))
            {
                if (!evidenceById.TryGetValue(nodeId, out var evidence) || !graphById.TryGetValue(nodeId, out var graphNode))
                {
                    continue;
                }

                var focus = evidence.StartLine == 0
                    ? Array.Empty<FocusRange>()
                    : claimant.Focus
                        .Where(range => string.Equals(range.NodeId, nodeId, StringComparison.Ordinal))
                        .Distinct()
                        .OrderBy(range => range.StartLine)
                        .ThenBy(range => range.EndLine)
                        .ToArray();
                citations.Add(new Citation(claimant.ClaimId, nodeId, evidence.Side, evidence.Path, graphNode.ObjectId,
                    evidence.StartLine, evidence.EndLine, focus, provenance));
            }
        }

        return citations;
    }

    private static IEnumerable<Claimant> EnumerateClaimants(MentalModel model)
    {
        if (model.Thesis is { } thesis)
        {
            yield return new Claimant(thesis.ClaimId, thesis.EvidenceNodeIds, thesis.Focus, true, false);
        }

        foreach (var track in model.Tracks)
        {
            if (track.Summary is { } summary)
            {
                yield return new Claimant(summary.ClaimId, summary.EvidenceNodeIds, summary.Focus, false, false);
            }

            foreach (var statement in track.OrderedSteps)
            {
                yield return new Claimant(statement.ClaimId, statement.EvidenceNodeIds, statement.Focus, false, false);
            }

            foreach (var purpose in track.Purposes)
            {
                yield return new Claimant(purpose.ClaimId, purpose.EvidenceNodeIds, purpose.Focus, false, false);
            }

            foreach (var relationship in track.Relationships)
            {
                yield return new Claimant(relationship.ClaimId, relationship.EvidenceNodeIds, relationship.Focus, false, false);
            }

            foreach (var participant in track.Participants)
            {
                var claimId = ClaimIds.Participant(track.Id, participant.Id);
                yield return new Claimant(claimId, participant.EvidenceNodeIds, [], false, true);
            }
        }
    }
}
