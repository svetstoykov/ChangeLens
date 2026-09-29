using ChangeLens.Core.FindingValidation.Models;

namespace ChangeLens.Core.Publication.Models;

/// <summary>Carries the review's result into publication: validated findings when it ran, or the reason it did not.</summary>
/// <param name="Status">The review status.</param>
/// <param name="Findings">The validated findings in reviewer draft order. Empty unless the review ran.</param>
/// <param name="WithheldCount">The number of findings validation removed. Zero unless the review ran.</param>
public sealed record PublicationReview(ReadingReviewStatus Status, IReadOnlyList<ValidatedFinding> Findings, int WithheldCount)
{
    /// <summary>Gets the review result for a review that was not run.</summary>
    public static PublicationReview NotRun { get; } = new(ReadingReviewStatus.NotRun, [], 0);

    /// <summary>Gets the review result for a review that failed.</summary>
    public static PublicationReview Failed { get; } = new(ReadingReviewStatus.Failed, [], 0);

    /// <summary>Gets the review result that drops the review's whole contribution because it did not fit.</summary>
    public static PublicationReview TooLarge { get; } = new(ReadingReviewStatus.TooLarge, [], 0);

    /// <summary>Creates the result of a review that ran from its finding validation outcome.</summary>
    /// <param name="validation">The finding validation outcome.</param>
    /// <returns>The review result carrying the validated findings and the withheld count.</returns>
    public static PublicationReview Ran(FindingValidationOutcome validation)
    {
        ArgumentNullException.ThrowIfNull(validation);
        return new PublicationReview(ReadingReviewStatus.Ran, validation.Findings, validation.WithheldCount);
    }
}
