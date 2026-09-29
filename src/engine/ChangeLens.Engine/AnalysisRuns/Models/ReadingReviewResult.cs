namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of the review outcome.
/// </summary>
/// <param name="Status">The review status wire value.</param>
/// <param name="Recommendation">The recommendation wire value, or <see langword="null" /> unless the review ran.</param>
/// <param name="WithheldCount">The number of findings validation removed.</param>
internal sealed record ReadingReviewResult(string Status, string? Recommendation, int WithheldCount);
