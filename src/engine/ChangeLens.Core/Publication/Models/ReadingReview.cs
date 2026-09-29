namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents the published outcome of the review.</summary>
/// <param name="Status">The review status.</param>
/// <param name="Recommendation">The derived recommendation, or <see langword="null" /> unless the review ran.</param>
/// <param name="WithheldCount">The number of findings validation removed. It is zero unless the review ran.</param>
public sealed record ReadingReview(ReadingReviewStatus Status, ReadingReviewRecommendation? Recommendation, int WithheldCount);
