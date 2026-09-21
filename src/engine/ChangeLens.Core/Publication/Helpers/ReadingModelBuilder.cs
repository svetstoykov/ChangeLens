using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.MentalModels.Helpers;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Publication.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.Publication.Helpers;

/// <summary>Builds the transferable reading model from exactly six publication inputs.</summary>
public static class ReadingModelBuilder
{
    /// <summary>Builds a reading model from comparison, model, citations, binder, graph, and checker facts.</summary>
    /// <param name="comparison">The comparison identity.</param>
    /// <param name="model">The published mental model.</param>
    /// <param name="citations">The claim-addressed citations.</param>
    /// <param name="binder">The evidence binder.</param>
    /// <param name="graph">The complete evidence graph.</param>
    /// <param name="facts">The explicit checker facts.</param>
    /// <returns>The reading model.</returns>
    public static ReadingModel Build(
        BinderComparison comparison,
        MentalModel model,
        IReadOnlyList<Citation> citations,
        EvidenceBinderModel binder,
        EvidenceGraphModel graph,
        CheckerFacts facts)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(citations);
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(facts);

        var claimIds = Claimants(model).ToArray();
        var duplicateIds = claimIds
            .GroupBy(claimId => claimId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var publishedCitations = citations
            .Where(citation => !duplicateIds.Contains(citation.ClaimId))
            .ToArray();
        var citationsByClaim = publishedCitations
            .GroupBy(citation => citation.ClaimId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Citation>)group.ToArray(), StringComparer.Ordinal);
        var areas = model.Tracks.Select(track => ToArea(track, citationsByClaim)).ToArray();
        var thesis = model.Thesis is { } statement ? ToStatement(statement, citationsByClaim) : null;
        var evidence = BuildEvidence(publishedCitations, binder);
        var omissionSummaries = BuildOmissionSummaries(binder, graph);
        var limitations = BuildLimitations(binder, graph);
        var assurances = BuildAssurances(model, binder, facts, claimIds, citationsByClaim, duplicateIds);
        return new ReadingModel(comparison, thesis, areas, publishedCitations, evidence, limitations, omissionSummaries, assurances);
    }

    private static ReadingArea ToArea(MentalModelTrack track, IReadOnlyDictionary<string, IReadOnlyList<Citation>> citations)
    {
        var participants = track.Participants.Select(ReadingParticipant.FromDraft).ToArray();
        var relationships = track.Relationships.Select(relationship => new ReadingRelationship(
            relationship.ClaimId,
            relationship.Id,
            relationship.FromParticipantId,
            relationship.ToParticipantId,
            relationship.Kind,
            relationship.Explanation,
            TrustFor(relationship.ClaimId, citations),
            relationship.EvidenceNodeIds)).ToArray();
        var steps = track.OrderedSteps.Select(statement => ToStatement(statement, citations)).ToArray();
        var purposes = track.Purposes.Select(statement => ToStatement(statement, citations)).ToArray();
        var summary = track.Summary is { } trackSummary ? ToStatement(trackSummary, citations) : null;
        var shape = Enum.TryParse<ReadingShape>(track.Shape, true, out var parsedShape)
            ? parsedShape
            : ReadingShape.ParticipantList;
        if (steps.Length == 0 && relationships.Length == 0 && purposes.Length == 0)
        {
            shape = ReadingShape.ParticipantList;
        }

        return new ReadingArea(track.Id, track.Title, summary, shape, participants, relationships, steps, purposes);
    }

    private static ReadingStatement ToStatement(MentalModelStatement statement, IReadOnlyDictionary<string, IReadOnlyList<Citation>> citations) =>
        new(statement.ClaimId, statement.Text, TrustFor(statement.ClaimId, citations), statement.EvidenceNodeIds);

    private static ReadingTrust TrustFor(string claimId, IReadOnlyDictionary<string, IReadOnlyList<Citation>> citations)
    {
        if (!citations.TryGetValue(claimId, out var claimCitations))
        {
            return ReadingTrust.Unchecked;
        }

        if (claimCitations.Any(citation => citation.Provenance == CitationProvenance.Checked))
        {
            return ReadingTrust.Checked;
        }

        return claimCitations.Any(citation => citation.Provenance == CitationProvenance.Derived)
            ? ReadingTrust.Derived
            : ReadingTrust.Unchecked;
    }

    private static IReadOnlyList<ReadingEvidence> BuildEvidence(IReadOnlyList<Citation> citations, EvidenceBinderModel binder)
    {
        var evidenceById = binder.Evidence.ToDictionary(evidence => evidence.NodeId, StringComparer.Ordinal);
        return citations
            .Select(citation => citation.NodeId)
            .Distinct(StringComparer.Ordinal)
            .Where(evidenceById.ContainsKey)
            .Select(nodeId =>
            {
                var evidence = evidenceById[nodeId];
                return new ReadingEvidence(evidence.NodeId, evidence.Path, evidence.Side, evidence.StartLine, evidence.EndLine,
                    evidence.IsChangedFile, evidence.IsRedacted, evidence.IsTruncated, evidence.Text);
            })
            .ToArray();
    }

    private static IReadOnlyList<ReadingOmissionSummary> BuildOmissionSummaries(EvidenceBinderModel binder, EvidenceGraphModel graph) =>
        binder.Omissions
            .Where(omission => omission.Kind is EvidenceBinderOmissionKind.FileNotRead or EvidenceBinderOmissionKind.NoEvidenceSelected
                or EvidenceBinderOmissionKind.PolicyExcluded)
            .OrderBy(omission => omission.Kind, StringComparer.Ordinal)
            .ThenBy(omission => omission.Reason, StringComparer.Ordinal)
            .Select(omission => new ReadingOmissionSummary(
                omission.Kind,
                omission.Reason,
                omission.Count,
                omission.Items.Count,
                ResolveSampleCount(omission, binder, graph)))
            .ToArray();

    private static int ResolveSampleCount(BinderOmission omission, EvidenceBinderModel binder, EvidenceGraphModel graph)
    {
        if (omission.Kind is EvidenceBinderOmissionKind.FileNotRead or EvidenceBinderOmissionKind.NoEvidenceSelected)
        {
            var paths = binder.ChangedFiles.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
            return omission.Items.Count(paths.Contains);
        }

        var nodeIds = graph.Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        return omission.Items.Count(nodeIds.Contains);
    }

    private static IReadOnlyList<ReadingLimitation> BuildLimitations(
        EvidenceBinderModel binder,
        EvidenceGraphModel graph)
    {
        var limitationDetails = new Dictionary<(ReadingLimitationKind Kind, string Path), HashSet<string>>();
        var changedFiles = binder.ChangedFiles.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var nodes = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        foreach (var omission in binder.Omissions)
        {
            var kind = omission.Kind switch
            {
                EvidenceBinderOmissionKind.FileNotRead => ReadingLimitationKind.FileNotRead,
                EvidenceBinderOmissionKind.NoEvidenceSelected => ReadingLimitationKind.FileNotQuoted,
                EvidenceBinderOmissionKind.PolicyExcluded => ReadingLimitationKind.FileNotQuoted,
                _ => (ReadingLimitationKind?)null,
            };
            if (kind is null)
            {
                continue;
            }

            foreach (var item in omission.Items)
            {
                var path = omission.Kind == EvidenceBinderOmissionKind.PolicyExcluded
                    ? nodes.TryGetValue(item, out var node) ? node.Path : null
                    : changedFiles.ContainsKey(item) ? item : null;
                if (path is null)
                {
                    continue;
                }

                var key = (kind.Value, path);
                if (!limitationDetails.TryGetValue(key, out var reasons))
                {
                    reasons = new HashSet<string>(StringComparer.Ordinal);
                    limitationDetails.Add(key, reasons);
                }

                reasons.Add(omission.Reason);
            }
        }

        var result = limitationDetails
            .OrderBy(pair => pair.Key.Kind)
            .ThenBy(pair => pair.Key.Path, StringComparer.Ordinal)
            .Select(pair => new ReadingLimitation(
                pair.Key.Kind,
                pair.Key.Path,
                string.Join("; ", pair.Value.OrderBy(reason => reason, StringComparer.Ordinal))))
            .ToList();
        var excluded = binder.Comparison.ExcludedUncommittedCounts;
        if (excluded.Total > 0)
        {
            result.Add(new ReadingLimitation(ReadingLimitationKind.UncommittedWorkExcluded, null,
                $"{excluded.Total} uncommitted lineages excluded ({excluded.Staged} staged, {excluded.Unstaged} unstaged, " +
                $"{excluded.Untracked} untracked, {excluded.Conflicted} conflicted)."));
        }

        return result;
    }

    private static IReadOnlyList<ReadingAssurance> BuildAssurances(
        MentalModel model,
        EvidenceBinderModel binder,
        CheckerFacts facts,
        IReadOnlyList<string> claimIds,
        IReadOnlyDictionary<string, IReadOnlyList<Citation>> citations,
        IReadOnlySet<string> duplicateIds)
    {
        var assurances = new List<ReadingAssurance>();
        if (!facts.Ran)
        {
            assurances.Add(new ReadingAssurance(ReadingAssuranceKind.CheckerNotRun, "Claim checking was not run."));
        }

        if (facts.Failed)
        {
            assurances.Add(new ReadingAssurance(ReadingAssuranceKind.CheckerFailed, "Claim checking failed or returned an unreadable reply."));
        }

        if (model.Tracks.SelectMany(track => track.Relationships).Any(relationship => relationship.Kind == "covers"))
        {
            assurances.Add(new ReadingAssurance(ReadingAssuranceKind.TestsNotExecuted, "The published model contains a covers relationship."));
        }

        assurances.Add(new ReadingAssurance(ReadingAssuranceKind.BuildNotExecuted, "The build was not executed by publication."));
        if (binder.Diagnostics.BudgetBinding || binder.Diagnostics.ChangedFilesWithoutEvidenceCount > 0)
        {
            assurances.Add(new ReadingAssurance(ReadingAssuranceKind.RepositoryNotFullyRead,
                "The repository evidence was bounded or at least one changed file has no evidence."));
        }

        foreach (var claimId in claimIds.Distinct(StringComparer.Ordinal))
        {
            if (!citations.ContainsKey(claimId))
            {
                assurances.Add(new ReadingAssurance(ReadingAssuranceKind.ClaimNotCited,
                    $"Claim '{claimId}' has no published citation.", claimId));
            }
        }

        foreach (var claimId in duplicateIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            assurances.Add(new ReadingAssurance(ReadingAssuranceKind.DuplicateClaimId,
                $"Claim id '{claimId}' occurred more than once; citations were withheld.", claimId));
        }

        return assurances;
    }

    private static IEnumerable<string> Claimants(MentalModel model)
    {
        if (model.Thesis is { } thesis)
        {
            yield return thesis.ClaimId;
        }

        foreach (var track in model.Tracks)
        {
            if (track.Summary is { } summary)
            {
                yield return summary.ClaimId;
            }

            foreach (var statement in track.OrderedSteps)
            {
                yield return statement.ClaimId;
            }

            foreach (var purpose in track.Purposes)
            {
                yield return purpose.ClaimId;
            }

            foreach (var relationship in track.Relationships)
            {
                yield return relationship.ClaimId;
            }

            foreach (var participant in track.Participants)
            {
                yield return ClaimIds.Participant(track.Id, participant.Id);
            }
        }
    }
}
