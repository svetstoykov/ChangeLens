using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.DraftValidation.Helpers;
using ChangeLens.Core.DraftValidation.Interfaces;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.DraftValidation.Services;

/// <summary>
///     Validates curator output against the evidence and contract disclosed by one binder.
/// </summary>
public sealed class DraftValidationService : IDraftValidationService
{
    private const string SupersedesKind = "supersedes";

    /// <inheritdoc />
    public DraftValidationOutcome Validate(MentalModelDraft draft, EvidenceBinderModel binder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(binder);
        cancellationToken.ThrowIfCancellationRequested();

        var nodeIds = binder.Evidence.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        var edgeIds = binder.MatchEdges.Select(edge => edge.EdgeId).ToHashSet(StringComparer.Ordinal);
        var relationshipKinds = CuratorContractConstants.RelationshipKinds.ToHashSet(StringComparer.Ordinal);
        var trackShapes = CuratorContractConstants.TrackShapes.ToHashSet(StringComparer.Ordinal);
        var matchGraph = MatchEdgeGraph.Build(binder.MatchEdges);
        var removals = new List<ValidationRemoval>();
        var invalidReferenceCount = 0;
        var invalidKindCount = 0;
        var uncitedEndpointCount = 0;
        var thesis = this.ValidateStatement(
            draft.Thesis,
            "thesis",
            nodeIds,
            binder.Contract.Limits.MaximumStatementCharacters,
            removals,
            ref invalidReferenceCount);
        var tracks = new List<DraftTrack>();
        var trackIds = new HashSet<string>(StringComparer.Ordinal);
        var limits = binder.Contract.Limits;

        foreach (var track in Capped(
                     draft.Tracks,
                     limits.MaximumTracks,
                     "track",
                     "tracks",
                     item => item.Id,
                     removals))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.TrackRejection(track, trackIds, trackShapes) is { } trackRejection)
            {
                removals.Add(new ValidationRemoval("track", track.Id, trackRejection));
                continue;
            }

            var summary = this.ValidateStatement(
                track.Summary,
                $"track:{track.Id}:summary",
                nodeIds,
                limits.MaximumStatementCharacters,
                removals,
                ref invalidReferenceCount);
            if (summary is null)
            {
                removals.Add(new ValidationRemoval("track", track.Id, "summary is not evidence-bound"));
                continue;
            }

            var participants = new List<DraftParticipant>();
            var participantIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var participant in Capped(
                         track.Participants,
                         limits.MaximumParticipantsPerTrack,
                         "participant",
                         "participants per track",
                         item => item.Id,
                         removals))
            {
                if (this.ParticipantRejection(participant, participantIds) is { } participantRejection)
                {
                    removals.Add(new ValidationRemoval("participant", participant.Id, participantRejection));
                    continue;
                }

                var kept = this.KeepDisclosedIds(
                    participant.EvidenceNodeIds,
                    nodeIds,
                    "participant",
                    participant.Id,
                    removals,
                    ref invalidReferenceCount);
                if (kept is not null)
                {
                    participants.Add(participant with { EvidenceNodeIds = kept });
                }
            }

            if (participants.Count == 0)
            {
                removals.Add(new ValidationRemoval("track", track.Id, "no valid participants remain"));
                continue;
            }

            var participantsById = participants.ToDictionary(participant => participant.Id, StringComparer.Ordinal);
            var relationships = new List<DraftRelationship>();
            var relationshipIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var relationship in Capped(
                         track.Relationships,
                         limits.MaximumRelationshipsPerTrack,
                         "relationship",
                         "relationships per track",
                         item => item.Id,
                         removals))
            {
                var keptNodes = this.KeepDisclosedIds(
                    relationship.EvidenceNodeIds,
                    nodeIds,
                    "relationship",
                    relationship.Id,
                    removals,
                    ref invalidReferenceCount);

                if (keptNodes is null)
                {
                    continue;
                }

                if (!relationshipKinds.Contains(relationship.Kind))
                {
                    invalidKindCount++;
                }

                var distinctMatchEdgeIds = Distinct(relationship.MatchEdgeIds);
                var candidate = relationship with
                {
                    EvidenceNodeIds = keptNodes,
                    MatchEdgeIds = distinctMatchEdgeIds,
                };
                if (this.RelationshipRejection(
                        candidate,
                        relationshipIds,
                        participantsById,
                        relationshipKinds,
                        limits.MaximumStatementCharacters,
                        edgeIds,
                        matchGraph,
                        ref invalidReferenceCount,
                        ref uncitedEndpointCount) is { } relationshipRejection)
                {
                    removals.Add(new ValidationRemoval("relationship", relationship.Id, relationshipRejection));
                    continue;
                }

                relationships.Add(candidate);
            }

            var steps = this.ValidateStatements(
                track.OrderedSteps,
                $"track:{track.Id}:step",
                nodeIds,
                limits.MaximumItemsPerTrack,
                limits.MaximumStatementCharacters,
                removals,
                ref invalidReferenceCount);
            var purposes = this.ValidateStatements(
                track.Purposes,
                $"track:{track.Id}:purpose",
                nodeIds,
                limits.MaximumItemsPerTrack,
                limits.MaximumStatementCharacters,
                removals,
                ref invalidReferenceCount);
            tracks.Add(track with
            {
                Summary = summary,
                Participants = participants,
                Relationships = relationships,
                OrderedSteps = steps,
                Purposes = purposes,
            });
        }

        var dropped = this.KeepDroppedNodeIds(draft.DroppedNodeIds, nodeIds, ref invalidReferenceCount);
        var cited = CitedNodeIds(draft);
        var undeclaredNodeCount = nodeIds.Count(nodeId => !cited.Contains(nodeId) && !dropped.Contains(nodeId));
        var validatedDraft = new MentalModelDraft(thesis ?? new BoundStatement(string.Empty, []), tracks, dropped);
        return new DraftValidationOutcome(
            validatedDraft,
            removals,
            invalidReferenceCount,
            invalidKindCount,
            undeclaredNodeCount,
            uncitedEndpointCount);
    }

    private static IEnumerable<T> Capped<T>(
        IReadOnlyList<T> items,
        int limit,
        string scope,
        string noun,
        Func<T, string> id,
        List<ValidationRemoval> removals)
    {
        var boundedLimit = Math.Max(0, limit);
        for (var index = boundedLimit; index < items.Count; index++)
        {
            removals.Add(new ValidationRemoval(scope, id(items[index]), $"beyond the {limit}-{noun} limit, so it was never read"));
        }

        return items.Take(boundedLimit);
    }

    private IReadOnlyList<BoundStatement> ValidateStatements(
        IReadOnlyList<BoundStatement> statements,
        string scope,
        HashSet<string> nodeIds,
        int limit,
        int maximumStatementCharacters,
        List<ValidationRemoval> removals,
        ref int invalidReferenceCount)
    {
        var boundedLimit = Math.Max(0, limit);
        for (var index = boundedLimit; index < statements.Count; index++)
        {
            removals.Add(new ValidationRemoval(
                "statement",
                $"{scope}:{index}",
                $"beyond the {limit}-item limit for orderedSteps and purposes, so it was never read"));
        }

        var valid = new List<BoundStatement>();
        foreach (var (statement, index) in statements.Take(boundedLimit).Select((statement, index) => (statement, index)))
        {
            var checkedStatement = this.ValidateStatement(
                statement,
                $"{scope}:{index}",
                nodeIds,
                maximumStatementCharacters,
                removals,
                ref invalidReferenceCount);
            if (checkedStatement is not null)
            {
                valid.Add(checkedStatement);
            }
        }

        return valid;
    }

    private BoundStatement? ValidateStatement(
        BoundStatement? statement,
        string scope,
        HashSet<string> nodeIds,
        int maximumStatementCharacters,
        List<ValidationRemoval> removals,
        ref int invalidReferenceCount)
    {
        if (StatementTextRejection(statement, maximumStatementCharacters) is { } rejection)
        {
            removals.Add(new ValidationRemoval("statement", scope, rejection));
            return null;
        }

        var kept = this.KeepDisclosedIds(
            statement!.EvidenceNodeIds,
            nodeIds,
            "statement",
            scope,
            removals,
            ref invalidReferenceCount);
        return kept is null ? null : statement with { EvidenceNodeIds = kept };
    }

    private IReadOnlyList<string>? KeepDisclosedIds(
        IReadOnlyList<string> cited,
        HashSet<string> nodeIds,
        string scope,
        string id,
        List<ValidationRemoval> removals,
        ref int invalidReferenceCount)
    {
        var distinct = Distinct(cited);
        var known = distinct.Where(nodeIds.Contains).ToArray();
        var unknown = distinct.Where(idValue => !nodeIds.Contains(idValue)).ToArray();
        invalidReferenceCount += unknown.Length;
        if (unknown.Length > 0 && known.Length == 0)
        {
            removals.Add(new ValidationRemoval(scope, id, $"cites only undisclosed evidence node id(s): {JoinIds(unknown)}"));
            return null;
        }

        if (unknown.Length > 0)
        {
            removals.Add(new ValidationRemoval(scope, id, $"stripped undisclosed evidence node id(s): {JoinIds(unknown)}"));
        }
        else if (known.Length == 0)
        {
            removals.Add(new ValidationRemoval(scope, id, "cites no evidence nodes"));
            return null;
        }

        return known;
    }

    private IReadOnlyList<string> KeepDroppedNodeIds(IReadOnlyList<string> droppedNodeIds, HashSet<string> nodeIds, ref int invalidReferenceCount)
    {
        var distinct = Distinct(droppedNodeIds);
        var known = distinct.Where(nodeIds.Contains).ToArray();
        invalidReferenceCount += distinct.Count - known.Length;
        return known;
    }

    private string? TrackRejection(
        DraftTrack track,
        HashSet<string> trackIds,
        HashSet<string> trackShapes)
    {
        if (!ValidIdentifier(track.Id))
        {
            return "id is empty, too long, or uses characters outside the allowed set";
        }

        if (!trackIds.Add(track.Id))
        {
            return "duplicate track id within the draft";
        }

        if (!this.HasText(track.Title))
        {
            return "title is empty";
        }

        if (!trackShapes.Contains(track.Shape))
        {
            return $"shape '{track.Shape}' is outside the closed vocabulary";
        }

        return null;
    }

    private string? ParticipantRejection(
        DraftParticipant participant,
        HashSet<string> participantIds)
    {
        if (!ValidIdentifier(participant.Id))
        {
            return "id is empty, too long, or uses characters outside the allowed set";
        }

        if (!participantIds.Add(participant.Id))
        {
            return "duplicate participant id within the track";
        }

        if (!this.HasText(participant.Name))
        {
            return "name is empty";
        }

        if (!this.HasText(participant.Role))
        {
            return "role is empty";
        }

        return null;
    }

    private string? RelationshipRejection(
        DraftRelationship relationship,
        HashSet<string> relationshipIds,
        IReadOnlyDictionary<string, DraftParticipant> participantsById,
        HashSet<string> relationshipKinds,
        int maximumStatementCharacters,
        HashSet<string> edgeIds,
        MatchEdgeGraph matchGraph,
        ref int invalidReferenceCount,
        ref int uncitedEndpointCount)
    {
        if (!ValidIdentifier(relationship.Id))
        {
            return "id is empty, too long, or uses characters outside the allowed set";
        }

        if (!relationshipIds.Add(relationship.Id))
        {
            return "duplicate relationship id within the track";
        }

        if (!this.ValidText(relationship.Explanation, maximumStatementCharacters))
        {
            return "explanation is empty or exceeds the statement character limit";
        }

        if (string.Equals(relationship.FromParticipantId, relationship.ToParticipantId, StringComparison.Ordinal))
        {
            return $"from and to are the same participant '{relationship.FromParticipantId}'";
        }

        if (!participantsById.TryGetValue(relationship.FromParticipantId, out var from))
        {
            return $"from participant '{relationship.FromParticipantId}' is not a surviving participant of this track";
        }

        if (!participantsById.TryGetValue(relationship.ToParticipantId, out var to))
        {
            return $"to participant '{relationship.ToParticipantId}' is not a surviving participant of this track";
        }

        if (!relationshipKinds.Contains(relationship.Kind))
        {
            return $"kind '{relationship.Kind}' is outside the closed vocabulary";
        }

        var unknownMatchEdgeIds = relationship.MatchEdgeIds.Where(id => !edgeIds.Contains(id)).Distinct(StringComparer.Ordinal).ToArray();
        invalidReferenceCount += unknownMatchEdgeIds.Length;
        if (unknownMatchEdgeIds.Length > 0)
        {
            return "cites match edge id(s) the binder never disclosed";
        }

        if (!HasEvidenceForBothEndpoints(relationship.EvidenceNodeIds, from, to))
        {
            uncitedEndpointCount++;
            return UncitedEndpointReason(relationship, from, to);
        }

        if (relationship.Kind == SupersedesKind && !matchGraph.HasPath(from, to))
        {
            return "'supersedes' requires a match edge chain linking the two participants' evidence, and the binder discloses none";
        }

        return null;
    }

    private bool ValidText(string? text, int maximumStatementCharacters) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= maximumStatementCharacters;

    private bool HasText(string? text) => !string.IsNullOrWhiteSpace(text);

    private static bool ValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 120
        && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');

    private static IReadOnlyList<string> Distinct(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).ToArray();

    private static string JoinIds(IReadOnlyList<string> ids) => string.Join(", ", ids.Take(5));

    private static bool HasEvidenceForBothEndpoints(
        IReadOnlyList<string> relationshipNodeIds,
        DraftParticipant from,
        DraftParticipant to)
    {
        var evidence = relationshipNodeIds.ToHashSet(StringComparer.Ordinal);
        return from.EvidenceNodeIds.Any(evidence.Contains) && to.EvidenceNodeIds.Any(evidence.Contains);
    }

    private static string UncitedEndpointReason(DraftRelationship relationship, DraftParticipant from, DraftParticipant to)
    {
        var evidence = relationship.EvidenceNodeIds.ToHashSet(StringComparer.Ordinal);
        var uncited = (from.EvidenceNodeIds.Any(evidence.Contains), to.EvidenceNodeIds.Any(evidence.Contains)) switch
        {
            (false, false) => $"either endpoint ('{from.Id}', '{to.Id}')",
            (false, true) => $"the from endpoint '{from.Id}'",
            _ => $"the to endpoint '{to.Id}'",
        };
        return $"cites no evidence node held by {uncited}";
    }

    private static HashSet<string> CitedNodeIds(MentalModelDraft draft)
    {
        var cited = new HashSet<string>(draft.Thesis?.EvidenceNodeIds ?? [], StringComparer.Ordinal);
        foreach (var track in draft.Tracks)
        {
            cited.UnionWith(track.Summary?.EvidenceNodeIds ?? []);
            foreach (var participant in track.Participants)
            {
                cited.UnionWith(participant.EvidenceNodeIds);
            }

            foreach (var relationship in track.Relationships)
            {
                cited.UnionWith(relationship.EvidenceNodeIds);
            }

            foreach (var step in track.OrderedSteps)
            {
                cited.UnionWith(step.EvidenceNodeIds);
            }

            foreach (var purpose in track.Purposes)
            {
                cited.UnionWith(purpose.EvidenceNodeIds);
            }
        }

        return cited;
    }

    private static string? StatementTextRejection(BoundStatement? statement, int maximumStatementCharacters)
    {
        if (statement is null)
        {
            return "no statement was supplied";
        }

        if (string.IsNullOrWhiteSpace(statement.Text))
        {
            return "text is empty";
        }

        return statement.Text.Length > maximumStatementCharacters
            ? $"text is {statement.Text.Length} characters, over the {maximumStatementCharacters}-character limit"
            : null;
    }
}
