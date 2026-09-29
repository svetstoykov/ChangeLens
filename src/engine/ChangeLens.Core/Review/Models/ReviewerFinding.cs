namespace ChangeLens.Core.Review.Models;

/// <summary>
///     Represents one finding proposed by the reviewer.
/// </summary>
/// <param name="Id">The finding identifier.</param>
/// <param name="Severity">The finding severity.</param>
/// <param name="Title">The short defect description.</param>
/// <param name="Trigger">The concrete condition that exposes the defect.</param>
/// <param name="Impact">The effect of the defect.</param>
/// <param name="Fix">The smallest appropriate remedy.</param>
/// <param name="EvidenceNodeIds">The binder evidence nodes cited by the finding.</param>
/// <param name="Anchor">The source quote and lines that focus the finding.</param>
public sealed record ReviewerFinding(
    string Id,
    string Severity,
    string Title,
    string Trigger,
    string Impact,
    string Fix,
    IReadOnlyList<string> EvidenceNodeIds,
    ReviewerAnchor Anchor);
