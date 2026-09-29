namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one published finding.
/// </summary>
/// <param name="Id">The finding identifier. Its citations use claim id <c>finding:{Id}</c>.</param>
/// <param name="Severity">The severity wire value.</param>
/// <param name="Title">The short defect statement.</param>
/// <param name="Trigger">The concrete input or state that causes the defect.</param>
/// <param name="Impact">What goes wrong, and under which conditions.</param>
/// <param name="Fix">The smallest appropriate remedy.</param>
/// <param name="AreaId">The attached area identifier, or <see langword="null" /> when the finding spans the change.</param>
internal sealed record ReadingFindingResult(string Id, string Severity, string Title, string Trigger, string Impact, string Fix, string? AreaId);
