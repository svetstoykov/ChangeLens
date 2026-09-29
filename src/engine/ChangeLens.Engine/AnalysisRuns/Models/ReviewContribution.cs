using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.Publication.Models;

namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents what the reviewer adds to a run: the review result that publication carries and the removal records
///     finding validation produced.
/// </summary>
/// <param name="Review">The review result. Cannot be <see langword="null" />.</param>
/// <param name="Removals">The reviewer's removal records in draft order. Empty unless the review ran. Cannot be <see langword="null" />.</param>
internal sealed record ReviewContribution(PublicationReview Review, IReadOnlyList<ValidationRemoval> Removals)
{
    /// <summary>Gets the contribution of a review that was not run.</summary>
    internal static ReviewContribution NotRun { get; } = new(PublicationReview.NotRun, []);

    /// <summary>Gets the contribution of a review that failed.</summary>
    internal static ReviewContribution Failed { get; } = new(PublicationReview.Failed, []);
}
