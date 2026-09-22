namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Represents deterministic claim-checking measurements and diagnostic claim ids.
/// </summary>
/// <param name="SubmittedCount">The number of claims submitted to the checker.</param>
/// <param name="RetainedCount">The number of claims retained after applying verdicts.</param>
/// <param name="RemovedCount">The number of claims removed.</param>
/// <param name="CorrectedCount">The number of relationships corrected.</param>
/// <param name="UncheckedCount">The number of submitted claims without a verdict.</param>
/// <param name="UnaddressableFocusCount">The number of claims removed because no requested focus resolved.</param>
/// <param name="DroppedRangeCount">The number of focus ranges that did not resolve.</param>
/// <param name="RefusedCorrectionCount">The number of refused relationship corrections.</param>
/// <param name="UnknownVerdictCount">The number of verdicts naming unknown claim ids.</param>
/// <param name="NarrowedCount">The number of claims retaining only part of requested focus.</param>
/// <param name="ParseFailure">The checker parse failure, or <see langword="null" />.</param>
/// <param name="RemovedClaimIds">The ordered removed claim ids.</param>
/// <param name="CorrectedClaimIds">The ordered corrected relationship claim ids.</param>
/// <param name="UncheckedClaimIds">The ordered unchecked claim ids.</param>
public sealed record ClaimCheckingSummary(
    int SubmittedCount,
    int RetainedCount,
    int RemovedCount,
    int CorrectedCount,
    int UncheckedCount,
    int UnaddressableFocusCount,
    int DroppedRangeCount,
    int RefusedCorrectionCount,
    int UnknownVerdictCount,
    int NarrowedCount,
    string? ParseFailure,
    IReadOnlyList<string> RemovedClaimIds,
    IReadOnlyList<string> CorrectedClaimIds,
    IReadOnlyList<string> UncheckedClaimIds)
{
    /// <summary>
    ///     Gets a value indicating whether the checker reply had a parse failure.
    /// </summary>
    public bool ParseFailed => this.ParseFailure is not null;
}
