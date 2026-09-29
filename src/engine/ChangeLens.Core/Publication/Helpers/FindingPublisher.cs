using ChangeLens.Core.FindingValidation.Constants;
using ChangeLens.Core.FindingValidation.Models;
using ChangeLens.Core.MentalModels.Helpers;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Publication.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.Publication.Helpers;

/// <summary>Attaches, orders, and cites validated findings, and derives the review outcome.</summary>
internal static class FindingPublisher
{
    private const string ClaimIdPrefix = "finding:";

    /// <summary>Publishes the review's findings against the published areas and their citations.</summary>
    /// <param name="review">The review result.</param>
    /// <param name="areas">The published areas in reading order.</param>
    /// <param name="areaCitations">The published explanation citations addressed by claim id.</param>
    /// <param name="binder">The evidence binder.</param>
    /// <param name="graph">The evidence graph.</param>
    /// <returns>The ordered findings, their citations, and the review outcome.</returns>
    internal static (IReadOnlyList<ReadingFinding> Findings, IReadOnlyList<Citation> Citations, ReadingReview Review) Publish(
        PublicationReview review,
        IReadOnlyList<ReadingArea> areas,
        IReadOnlyDictionary<string, IReadOnlyList<Citation>> areaCitations,
        EvidenceBinderModel binder,
        EvidenceGraphModel graph)
    {
        if (review.Status != ReadingReviewStatus.Ran)
        {
            return ([], [], new ReadingReview(review.Status, null, 0));
        }

        var areaNodeIds = areas.Select(area => AreaNodeIds(area, areaCitations)).ToArray();
        var attached = review.Findings
            .Select(validated => (Validated: validated, AreaIndex: AttachedAreaIndex(validated, areaNodeIds)))
            .OrderBy(entry => Rank(entry.Validated.Finding.Severity))
            .ThenBy(entry => entry.AreaIndex ?? int.MaxValue)
            .ThenBy(entry => entry.Validated.DraftPosition)
            .ToArray();
        var findings = attached
            .Select(entry => ToFinding(entry.Validated, entry.AreaIndex is { } index ? areas[index].Id : null))
            .ToArray();
        var citations = attached.SelectMany(entry => CiteFinding(entry.Validated, binder, graph)).ToArray();
        var outcome = new ReadingReview(ReadingReviewStatus.Ran, Recommend(findings, review.WithheldCount), review.WithheldCount);
        return (findings, citations, outcome);
    }

    private static ReadingReviewRecommendation Recommend(IReadOnlyList<ReadingFinding> findings, int withheldCount)
    {
        if (findings.Any(finding => finding.Severity == ReadingFindingSeverity.Critical))
        {
            return ReadingReviewRecommendation.DefectsToFix;
        }

        if (findings.Any(finding => finding.Severity == ReadingFindingSeverity.Warning))
        {
            return ReadingReviewRecommendation.IssuesWorthAddressing;
        }

        return withheldCount == 0 ? ReadingReviewRecommendation.NoDefectsFound : ReadingReviewRecommendation.NoDefectsConfirmed;
    }

    private static HashSet<string> AreaNodeIds(ReadingArea area, IReadOnlyDictionary<string, IReadOnlyList<Citation>> citations)
    {
        var claimIds = area.OrderedSteps.Select(statement => statement.ClaimId)
            .Concat(area.Purposes.Select(statement => statement.ClaimId))
            .Concat(area.Relationships.Select(relationship => relationship.ClaimId))
            .Concat(area.Participants.Select(participant => ClaimIds.Participant(area.Id, participant.Id)));
        if (area.Summary is { } summary)
        {
            claimIds = claimIds.Append(summary.ClaimId);
        }

        return claimIds
            .Where(citations.ContainsKey)
            .SelectMany(claimId => citations[claimId])
            .Select(citation => citation.NodeId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static int? AttachedAreaIndex(ValidatedFinding validated, IReadOnlyList<HashSet<string>> areaNodeIds)
    {
        var nodeIds = validated.Finding.EvidenceNodeIds.Distinct(StringComparer.Ordinal).ToArray();
        int? best = null;
        var bestOverlap = 0;
        for (var index = 0; index < areaNodeIds.Count; index++)
        {
            var overlap = nodeIds.Count(areaNodeIds[index].Contains);
            if (overlap > bestOverlap)
            {
                best = index;
                bestOverlap = overlap;
            }
        }

        return best;
    }

    private static IEnumerable<Citation> CiteFinding(ValidatedFinding validated, EvidenceBinderModel binder, EvidenceGraphModel graph)
    {
        var graphById = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var evidenceById = binder.Evidence.ToDictionary(evidence => evidence.NodeId, StringComparer.Ordinal);
        var finding = validated.Finding;
        foreach (var nodeId in finding.EvidenceNodeIds.Distinct(StringComparer.Ordinal))
        {
            if (!evidenceById.TryGetValue(nodeId, out var evidence) || !graphById.TryGetValue(nodeId, out var graphNode))
            {
                continue;
            }

            IReadOnlyList<FocusRange> focus = string.Equals(nodeId, finding.Anchor.NodeId, StringComparison.Ordinal)
                ? [new FocusRange(nodeId, validated.FocusRange.StartLine, validated.FocusRange.EndLine)]
                : [];
            yield return new Citation(ClaimIdPrefix + finding.Id, nodeId, evidence.Side, evidence.Path, graphNode.ObjectId,
                evidence.StartLine, evidence.EndLine, focus, CitationProvenance.Unchecked);
        }
    }

    private static ReadingFinding ToFinding(ValidatedFinding validated, string? areaId)
    {
        var finding = validated.Finding;
        var severity = finding.Severity switch
        {
            FindingValidationConstants.CriticalSeverity => ReadingFindingSeverity.Critical,
            FindingValidationConstants.WarningSeverity => ReadingFindingSeverity.Warning,
            _ => ReadingFindingSeverity.Info,
        };
        return new ReadingFinding(finding.Id, severity, finding.Title, finding.Trigger, finding.Impact, finding.Fix, areaId);
    }

    private static int Rank(string severity) => severity switch
    {
        FindingValidationConstants.CriticalSeverity => 0,
        FindingValidationConstants.WarningSeverity => 1,
        _ => 2,
    };
}
