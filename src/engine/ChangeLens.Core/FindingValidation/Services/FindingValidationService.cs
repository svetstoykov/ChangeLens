using System.Diagnostics;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.FindingValidation.Constants;
using ChangeLens.Core.FindingValidation.Interfaces;
using ChangeLens.Core.FindingValidation.Models;
using ChangeLens.Core.Review.Models;
using Microsoft.Extensions.Logging;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.FindingValidation.Services;

/// <summary>
///     Applies the publication rules to every finding in draft order.
/// </summary>
public sealed class FindingValidationService : IFindingValidationService
{
    private static readonly HashSet<string> AllowedSeverities = new(StringComparer.Ordinal)
    {
        "critical", "warning", "info",
    };

    private readonly ILogger<FindingValidationService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FindingValidationService" /> class.
    /// </summary>
    /// <param name="logger">The logger for validation outcomes. Cannot be <see langword="null" />.</param>
    public FindingValidationService(ILogger<FindingValidationService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this._logger = logger;
    }

    /// <inheritdoc />
    public FindingValidationOutcome Validate(ReviewerDraft draft, EvidenceBinderModel binder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(binder);
        cancellationToken.ThrowIfCancellationRequested();

        var started = Stopwatch.GetTimestamp();
        var evidenceById = (binder.Evidence ?? []).ToDictionary(evidence => evidence.NodeId, StringComparer.Ordinal);
        var findings = new List<ValidatedFinding>();
        var removals = new List<ValidationRemoval>();
        var reservedIds = new HashSet<string>(StringComparer.Ordinal);
        var incoming = draft.Findings ?? Array.Empty<ReviewerFinding>();

        for (var index = 0; index < incoming.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var finding = incoming[index];
            var hasWellFormedId = finding is not null && ValidIdentifier(finding.Id);
            var id = hasWellFormedId ? finding!.Id : $"findings[{index}]";
            var uniqueId = hasWellFormedId && reservedIds.Add(finding!.Id);

            if (index >= FindingValidationConstants.MaximumFindings)
            {
                removals.Add(Removal(id, FindingValidationConstants.OverCapReason));
                continue;
            }

            if (!hasWellFormedId || !uniqueId || !HasValidFields(finding, binder.Contract.Limits.MaximumStatementCharacters))
            {
                removals.Add(Removal(id, FindingValidationConstants.InvalidFieldReason));
                continue;
            }

            var validFinding = finding!;
            if (validFinding.EvidenceNodeIds.Any(nodeId => nodeId is null || !evidenceById.ContainsKey(nodeId)))
            {
                removals.Add(Removal(id, FindingValidationConstants.UndisclosedEvidenceReason));
                continue;
            }

            var citedEvidence = validFinding.EvidenceNodeIds.Select(nodeId => evidenceById[nodeId]).ToArray();
            if (!citedEvidence.Any(evidence => evidence.IsChangedFile))
            {
                removals.Add(Removal(id, FindingValidationConstants.NoChangedFileCitationReason));
                continue;
            }

            if (validFinding.Anchor is null
                || validFinding.Anchor.NodeId is null
                || !validFinding.EvidenceNodeIds.Contains(validFinding.Anchor.NodeId, StringComparer.Ordinal)
                || !evidenceById.TryGetValue(validFinding.Anchor.NodeId, out var anchorEvidence)
                || !HasSourceLineRange(anchorEvidence)
                || string.IsNullOrWhiteSpace(validFinding.Anchor.Lines))
            {
                removals.Add(Removal(id, FindingValidationConstants.AnchorMismatchReason));
                continue;
            }

            var anchorMatch = FindAnchor(anchorEvidence, validFinding.Anchor.Lines);
            if (anchorMatch.MatchCount == 0)
            {
                removals.Add(Removal(id, FindingValidationConstants.AnchorMismatchReason));
                continue;
            }

            if (anchorMatch.MatchCount > 1)
            {
                removals.Add(Removal(id, FindingValidationConstants.AnchorAmbiguousReason));
                continue;
            }

            var focusStartLine = checked(anchorEvidence.StartLine + anchorMatch.StartOffset);
            var focusEndLine = checked(focusStartLine + anchorMatch.LineCount - 1);
            if (focusEndLine > anchorEvidence.EndLine)
            {
                removals.Add(Removal(id, FindingValidationConstants.AnchorMismatchReason));
                continue;
            }

            findings.Add(new ValidatedFinding(validFinding, new FindingFocusRange(focusStartLine, focusEndLine), index));
        }

        var outcome = new FindingValidationOutcome(findings, removals);
        this._logger.LogInformation(
            "Finding validation completed with {PublishedFindings} published and {WithheldFindings} withheld from "
            + "{IncomingFindings} in {ElapsedMilliseconds:0.000} ms with removal reasons {RemovalReasons}.",
            outcome.Findings.Count, outcome.WithheldCount, incoming.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            outcome.Removals.Select(removal => removal.Reason));
        return outcome;
    }

    private static ValidationRemoval Removal(string id, string reason) => new(FindingValidationConstants.FindingScope, id, reason);

    private static bool HasValidFields(ReviewerFinding? finding, int maximumStatementCharacters) =>
        finding is not null
        && AllowedSeverities.Contains(finding.Severity)
        && ValidText(finding.Title, FindingValidationConstants.MaximumTitleCharacters)
        && ValidText(finding.Trigger, maximumStatementCharacters)
        && ValidText(finding.Impact, maximumStatementCharacters)
        && ValidText(finding.Fix, maximumStatementCharacters)
        && finding.EvidenceNodeIds is { Count: > 0 };

    private static bool ValidText(string? value, int maximumCharacters) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumCharacters;

    private static bool ValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= FindingValidationConstants.MaximumIdCharacters
        && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');

    private static bool HasSourceLineRange(BinderEvidence evidence) => evidence.StartLine > 0 && evidence.EndLine >= evidence.StartLine;

    /// <summary>
    ///     Finds every exact whole-line occurrence of the anchor in the disclosed quote.
    /// </summary>
    /// <param name="evidence">The source quote to search.</param>
    /// <param name="anchorText">The whole source lines copied by the reviewer.</param>
    /// <returns>The occurrence count, first line offset, and number of matched lines.</returns>
    /// <remarks>
    ///     Trailing anchor line endings are removed, line endings are normalized, and trailing whitespace is ignored on each line.
    ///     Remaining text is compared with <see cref="StringComparison.Ordinal" />. The offset is zero-based within the quote.
    /// </remarks>
    private static (int MatchCount, int StartOffset, int LineCount) FindAnchor(BinderEvidence evidence, string anchorText)
    {
        var sourceLines = Lines(evidence.Text);
        var anchorLines = Lines(anchorText.TrimEnd('\r', '\n'));
        if (anchorLines.Length == 0 || sourceLines.Length < anchorLines.Length)
        {
            return (0, -1, anchorLines.Length);
        }

        var matchCount = 0;
        var startOffset = -1;
        for (var offset = 0; offset <= sourceLines.Length - anchorLines.Length; offset++)
        {
            var matches = true;
            for (var lineIndex = 0; lineIndex < anchorLines.Length; lineIndex++)
            {
                if (!string.Equals(sourceLines[offset + lineIndex], anchorLines[lineIndex], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                matchCount++;
                startOffset = offset;
            }
        }

        return (matchCount, startOffset, anchorLines.Length);
    }

    /// <summary>
    ///     Splits text into lines after normalizing line endings and removing trailing whitespace.
    /// </summary>
    /// <param name="text">The source quote or anchor text.</param>
    /// <returns>The normalized lines with leading characters preserved.</returns>
    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToArray();
}
