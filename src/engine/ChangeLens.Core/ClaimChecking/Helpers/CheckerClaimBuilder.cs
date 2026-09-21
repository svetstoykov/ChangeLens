using System.Globalization;
using System.Text;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.MentalModels.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.ClaimChecking.Helpers;

/// <summary>
///     Builds isolated checker claims from a published mental model and its binder.
/// </summary>
internal static class CheckerClaimBuilder
{
    /// <summary>
    ///     Builds claims in published track order.
    /// </summary>
    /// <param name="model">The published mental model.</param>
    /// <param name="binder">The binder that supplied evidence.</param>
    /// <returns>The ordered checker claims.</returns>
    internal static IReadOnlyList<CheckerClaim> Build(MentalModel model, EvidenceBinderModel binder)
    {
        var evidence = binder.Evidence.ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        var claims = new List<CheckerClaim>();
        foreach (var track in model.Tracks)
        {
            if (track.Summary is not null)
            {
                claims.Add(Statement(track.Summary, "summary", evidence));
            }

            foreach (var step in track.OrderedSteps)
            {
                claims.Add(Statement(step, "step", evidence));
            }

            foreach (var purpose in track.Purposes)
            {
                claims.Add(Statement(purpose, "purpose", evidence));
            }

            var participants = track.Participants.ToDictionary(participant => participant.Id, StringComparer.Ordinal);
            foreach (var relationship in track.Relationships)
            {
                if (!participants.TryGetValue(relationship.FromParticipantId, out var from)
                    || !participants.TryGetValue(relationship.ToParticipantId, out var to))
                {
                    continue;
                }

                var fromText = ParticipantText(from);
                var toText = ParticipantText(to);
                var text = $"{from.Name} {relationship.Kind} {to.Name}{Environment.NewLine}Explanation: {relationship.Explanation}";
                var quotes = Quotes(relationship.EvidenceNodeIds, evidence, from, to);
                claims.Add(new CheckerClaim(
                    relationship.ClaimId,
                    "relationship",
                    text,
                    relationship.Kind,
                    fromText,
                    toText,
                    quotes));
            }
        }

        return claims;
    }

    private static CheckerClaim Statement(
        MentalModelStatement statement,
        string type,
        IReadOnlyDictionary<string, BinderEvidence> evidence) =>
        new(statement.ClaimId, type, statement.Text, null, null, null, Quotes(statement.EvidenceNodeIds, evidence, null, null));

    private static IReadOnlyList<CheckerQuote> Quotes(
        IReadOnlyList<string> nodeIds,
        IReadOnlyDictionary<string, BinderEvidence> evidence,
        DraftParticipant? from,
        DraftParticipant? to)
    {
        var quotes = new List<CheckerQuote>();
        foreach (var nodeId in nodeIds.Distinct(StringComparer.Ordinal))
        {
            if (!evidence.TryGetValue(nodeId, out var item))
            {
                continue;
            }

            var role = RoleFor(item.NodeId, from, to);
            quotes.Add(new CheckerQuote(
                item.NodeId,
                item.Path,
                item.Side,
                item.StartLine,
                item.EndLine,
                role,
                NumberedText(item)));
        }

        return quotes;
    }

    private static string? RoleFor(string nodeId, DraftParticipant? from, DraftParticipant? to)
    {
        if (from is not null && from.EvidenceNodeIds.Contains(nodeId, StringComparer.Ordinal))
        {
            return "from";
        }

        if (to is not null && to.EvidenceNodeIds.Contains(nodeId, StringComparer.Ordinal))
        {
            return "to";
        }

        return from is null && to is null ? null : "context";
    }

    private static string NumberedText(BinderEvidence evidence)
    {
        if (evidence.StartLine == 0)
        {
            return evidence.Text;
        }

        var lines = evidence.Text.Split('\n');
        var widest = (evidence.StartLine + lines.Length - 1).ToString(CultureInfo.InvariantCulture).Length;
        var builder = new StringBuilder(evidence.Text.Length + lines.Length * (widest + 3));
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            builder.Append((evidence.StartLine + index).ToString(CultureInfo.InvariantCulture).PadLeft(widest)).Append("| ");
            builder.Append(lines[index].TrimEnd('\r'));
        }

        return builder.ToString();
    }

    private static string ParticipantText(DraftParticipant participant) => $"{participant.Name} ({participant.Role})";
}
