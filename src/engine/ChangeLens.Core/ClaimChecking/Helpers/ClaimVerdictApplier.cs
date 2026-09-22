using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.DraftValidation.Helpers;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.MentalModels.Helpers;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.ClaimChecking.Helpers;

/// <summary>
///     Applies raw checker verdicts while preserving published claim identities.
/// </summary>
internal static class ClaimVerdictApplier
{
    /// <summary>
    ///     Applies a parsed checker reply to a published mental model.
    /// </summary>
    /// <param name="model">The model whose claims were submitted.</param>
    /// <param name="claims">The claims submitted to the checker.</param>
    /// <param name="reply">The parsed checker reply.</param>
    /// <param name="binder">The binder used to screen corrections and resolve focus.</param>
    /// <returns>The checked model and deterministic summary.</returns>
    internal static ClaimCheckingOutcome Apply(
        MentalModel model,
        IReadOnlyList<CheckerClaim> claims,
        ClaimCheckerReply reply,
        EvidenceBinderModel binder)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(reply);
        ArgumentNullException.ThrowIfNull(binder);

        if (reply.ParseFailure is not null)
        {
            return ParseFailure(claims, reply.ParseFailure);
        }

        var claimsById = claims.ToDictionary(claim => claim.ClaimId, StringComparer.Ordinal);
        var verdictsById = reply.Verdicts
            .Where(verdict => verdict.ClaimId is not null && claimsById.ContainsKey(verdict.ClaimId))
            .GroupBy(verdict => verdict.ClaimId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var unknownVerdictCount = reply.Verdicts.Count(verdict => verdict.ClaimId is null || !claimsById.ContainsKey(verdict.ClaimId));
        var removedIds = new List<string>();
        var uncheckedIds = new List<string>();
        var correctedIds = new List<string>();
        var resolvedFocus = new Dictionary<string, IReadOnlyList<FocusRange>>(StringComparer.Ordinal);
        var droppedRangeCount = 0;
        var unaddressableFocusCount = 0;
        var narrowedCount = 0;

        foreach (var claim in claims)
        {
            if (!verdictsById.TryGetValue(claim.ClaimId, out var verdicts) || verdicts.Length != 1)
            {
                uncheckedIds.Add(claim.ClaimId);
                continue;
            }

            var verdict = verdicts[0];
            var focus = ResolveFocus(claim, verdict.Focus, ref droppedRangeCount, ref unaddressableFocusCount, ref narrowedCount);
            if (focus is null)
            {
                AddRemoved(removedIds, claim.ClaimId);
                continue;
            }

            resolvedFocus[claim.ClaimId] = focus;
        }

        var screened = ScreenCorrections(model, claimsById, verdictsById, binder, removedIds);
        var tracks = new List<MentalModelTrack>();
        foreach (var track in model.Tracks)
        {
            var summary = ApplyStatement(track.Summary, verdictsById, resolvedFocus, removedIds);
            var steps = track.OrderedSteps
                .Select(statement => ApplyStatement(statement, verdictsById, resolvedFocus, removedIds))
                .OfType<MentalModelStatement>()
                .ToArray();
            var purposes = track.Purposes
                .Select(statement => ApplyStatement(statement, verdictsById, resolvedFocus, removedIds))
                .OfType<MentalModelStatement>()
                .ToArray();
            var relationships = track.Relationships
                .Select(relationship => ApplyRelationship(
                    relationship,
                    verdictsById,
                    resolvedFocus,
                    screened.Accepted,
                    removedIds,
                    correctedIds))
                .OfType<MentalModelRelationship>()
                .ToArray();

            if (relationships.Length == 0 && steps.Length == 0 && purposes.Length == 0)
            {
                continue;
            }

            var participants = RetainParticipants(track, summary, steps, purposes, relationships);
            if (participants.Count == 0)
            {
                continue;
            }

            tracks.Add(track with
            {
                Summary = summary,
                Participants = participants,
                Relationships = relationships,
                OrderedSteps = steps,
                Purposes = purposes,
            });
        }

        var result = tracks.Count == 0 ? MentalModel.Empty : new MentalModel(DeriveThesis(tracks), tracks);
        var retainedCount = ClaimCount(result);
        var removedClaimIds = claims.Select(claim => claim.ClaimId).Where(removedIds.Contains).ToArray();
        var correctedClaimIds = claims.Select(claim => claim.ClaimId).Where(correctedIds.Contains).ToArray();
        var summaryResult = new ClaimCheckingSummary(
            claims.Count,
            retainedCount,
            removedClaimIds.Length,
            correctedClaimIds.Length,
            uncheckedIds.Count,
            unaddressableFocusCount,
            droppedRangeCount,
            screened.RefusedCorrectionCount,
            unknownVerdictCount,
            narrowedCount,
            null,
            removedClaimIds,
            correctedClaimIds,
            uncheckedIds);
        return new ClaimCheckingOutcome(result, summaryResult);
    }

    private static ClaimCheckingOutcome ParseFailure(IReadOnlyList<CheckerClaim> claims, string failure)
    {
        var uncheckedIds = claims.Select(claim => claim.ClaimId).ToArray();
        var summary = new ClaimCheckingSummary(
            claims.Count,
            0,
            0,
            0,
            claims.Count,
            0,
            0,
            0,
            0,
            0,
            failure,
            [],
            [],
            uncheckedIds);
        return new ClaimCheckingOutcome(MentalModel.Empty, summary);
    }

    private static IReadOnlyList<FocusRange>? ResolveFocus(
        CheckerClaim claim,
        IReadOnlyList<FocusRange> requested,
        ref int droppedRangeCount,
        ref int unaddressableFocusCount,
        ref int narrowedCount)
    {
        if (requested.Count == 0)
        {
            return [];
        }

        var quotes = claim.Quotes
            .GroupBy(quote => quote.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var resolved = new HashSet<FocusRange>();
        var claimDroppedRangeCount = 0;
        foreach (var range in requested)
        {
            if (range.StartLine < 1
                || range.EndLine < range.StartLine
                || !quotes.TryGetValue(range.NodeId, out var quote)
                || quote.StartLine < 1
                || range.StartLine < quote.StartLine
                || range.EndLine > quote.EndLine)
            {
                droppedRangeCount++;
                claimDroppedRangeCount++;
                continue;
            }

            resolved.Add(range);
        }

        if (resolved.Count == 0)
        {
            unaddressableFocusCount++;
            return null;
        }

        if (claimDroppedRangeCount > 0)
        {
            narrowedCount++;
        }

        return resolved
            .OrderBy(range => range.NodeId, StringComparer.Ordinal)
            .ThenBy(range => range.StartLine)
            .ThenBy(range => range.EndLine)
            .ToArray();
    }

    private static CorrectionScreen ScreenCorrections(
        MentalModel model,
        IReadOnlyDictionary<string, CheckerClaim> claimsById,
        IReadOnlyDictionary<string, ClaimVerdict[]> verdictsById,
        EvidenceBinderModel binder,
        List<string> removedIds)
    {
        var accepted = new Dictionary<string, ClaimVerdict>(StringComparer.Ordinal);
        foreach (var pair in verdictsById)
        {
            if (pair.Value.Length == 1)
            {
                accepted[pair.Key] = pair.Value[0];
            }
        }

        var graph = MatchEdgeGraph.Build(binder.MatchEdges);
        var refused = 0;
        foreach (var track in model.Tracks)
        {
            var participants = track.Participants.ToDictionary(participant => participant.Id, StringComparer.Ordinal);
            foreach (var relationship in track.Relationships)
            {
                if (!accepted.TryGetValue(relationship.ClaimId, out var verdict)
                    || verdict.Kind != ClaimVerdictKind.WrongKind
                    || !claimsById.ContainsKey(relationship.ClaimId))
                {
                    continue;
                }

                if (!CuratorContractConstants.RelationshipKinds.Contains(verdict.CorrectedKind ?? string.Empty, StringComparer.Ordinal)
                    || string.Equals(verdict.CorrectedKind, relationship.Kind, StringComparison.Ordinal))
                {
                    accepted.Remove(relationship.ClaimId);
                    AddRemoved(removedIds, relationship.ClaimId);
                    continue;
                }

                if (!string.Equals(verdict.CorrectedKind, CuratorContractConstants.Supersedes, StringComparison.Ordinal))
                {
                    continue;
                }

                if (participants.TryGetValue(relationship.FromParticipantId, out var from)
                    && participants.TryGetValue(relationship.ToParticipantId, out var to)
                    && graph.HasPath(from, to))
                {
                    continue;
                }

                accepted.Remove(relationship.ClaimId);
                AddRemoved(removedIds, relationship.ClaimId);
                refused++;
            }
        }

        return new CorrectionScreen(accepted, refused);
    }

    private static MentalModelStatement? ApplyStatement(
        MentalModelStatement? statement,
        IReadOnlyDictionary<string, ClaimVerdict[]> verdictsById,
        IReadOnlyDictionary<string, IReadOnlyList<FocusRange>> resolvedFocus,
        List<string> removedIds)
    {
        if (statement is null)
        {
            return null;
        }

        if (!verdictsById.TryGetValue(statement.ClaimId, out var verdicts) || verdicts.Length != 1)
        {
            return null;
        }

        if (verdicts[0].Kind != ClaimVerdictKind.Supported || !resolvedFocus.TryGetValue(statement.ClaimId, out var focus))
        {
            AddRemoved(removedIds, statement.ClaimId);
            return null;
        }

        return statement with { Focus = focus };
    }

    private static MentalModelRelationship? ApplyRelationship(
        MentalModelRelationship relationship,
        IReadOnlyDictionary<string, ClaimVerdict[]> verdictsById,
        IReadOnlyDictionary<string, IReadOnlyList<FocusRange>> resolvedFocus,
        IReadOnlyDictionary<string, ClaimVerdict> screened,
        List<string> removedIds,
        List<string> correctedIds)
    {
        if (!screened.TryGetValue(relationship.ClaimId, out var verdict)
            || !verdictsById.TryGetValue(relationship.ClaimId, out var verdicts)
            || verdicts.Length != 1
            || !resolvedFocus.TryGetValue(relationship.ClaimId, out var focus))
        {
            return null;
        }

        if (verdict.Kind == ClaimVerdictKind.Supported)
        {
            return relationship with { Focus = focus };
        }

        if (verdict.Kind != ClaimVerdictKind.WrongKind || verdict.CorrectedKind is null)
        {
            AddRemoved(removedIds, relationship.ClaimId);
            return null;
        }

        correctedIds.Add(relationship.ClaimId);
        return relationship with
        {
            Kind = verdict.CorrectedKind,
            Explanation = CorrectionExplanation(relationship, verdict.CorrectedKind),
            MatchEdgeIds = [],
            Focus = focus,
        };
    }

    private static IReadOnlyList<DraftParticipant> RetainParticipants(
        MentalModelTrack track,
        MentalModelStatement? summary,
        IReadOnlyList<MentalModelStatement> steps,
        IReadOnlyList<MentalModelStatement> purposes,
        IReadOnlyList<MentalModelRelationship> relationships)
    {
        var endpointIds = relationships
            .SelectMany(relationship => new[] { relationship.FromParticipantId, relationship.ToParticipantId })
            .ToHashSet(StringComparer.Ordinal);
        var citedNodes = (summary is null ? [] : new[] { summary })
            .Concat(steps)
            .Concat(purposes)
            .SelectMany(statement => statement.EvidenceNodeIds)
            .ToHashSet(StringComparer.Ordinal);
        return track.Participants
            .Where(participant => endpointIds.Contains(participant.Id) || participant.EvidenceNodeIds.Any(citedNodes.Contains))
            .ToArray();
    }

    private static MentalModelStatement? DeriveThesis(IReadOnlyList<MentalModelTrack> tracks)
    {
        var headlines = tracks.Select(Headline).OfType<MentalModelStatement>().ToArray();
        if (headlines.Length == 0)
        {
            return null;
        }

        if (headlines.Length == 1)
        {
            return headlines[0] with { ClaimId = ClaimIds.Thesis };
        }

        var nodes = headlines.SelectMany(headline => headline.EvidenceNodeIds).Distinct(StringComparer.Ordinal).ToArray();
        return new MentalModelStatement(ClaimIds.Thesis, string.Join(' ', headlines.Select(headline => headline.Text)), nodes, []);
    }

    private static MentalModelStatement? Headline(MentalModelTrack track) =>
        track.Summary ?? track.Purposes.FirstOrDefault() ?? track.OrderedSteps.FirstOrDefault();

    private static string CorrectionExplanation(MentalModelRelationship relationship, string correctedKind) =>
        $"Kind corrected to {correctedKind} by the checker. The curator's explanation described a "
        + $"{relationship.Kind} relationship and was removed with it.";

    private static void AddRemoved(List<string> removedIds, string claimId)
    {
        if (!removedIds.Contains(claimId, StringComparer.Ordinal))
        {
            removedIds.Add(claimId);
        }
    }

    private static int ClaimCount(MentalModel model)
    {
        var count = 0;
        foreach (var track in model.Tracks)
        {
            count += track.Summary is null ? 0 : 1;
            count += track.OrderedSteps.Count + track.Purposes.Count + track.Relationships.Count;
        }

        return count;
    }

}
