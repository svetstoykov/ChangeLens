namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents one published finding. Its citations are addressed by claim id <c>finding:{Id}</c>.</summary>
/// <param name="Id">The finding identifier.</param>
/// <param name="Severity">The finding severity.</param>
/// <param name="Title">The short defect statement.</param>
/// <param name="Trigger">The concrete input or state that causes the defect.</param>
/// <param name="Impact">What goes wrong, and under which conditions.</param>
/// <param name="Fix">The smallest appropriate remedy.</param>
/// <param name="AreaId">The attached area identifier, or <see langword="null" /> when the finding is not attached to an area.</param>
public sealed record ReadingFinding(
    string Id,
    ReadingFindingSeverity Severity,
    string Title,
    string Trigger,
    string Impact,
    string Fix,
    string? AreaId);
