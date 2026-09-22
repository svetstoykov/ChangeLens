namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of a published reading model.
/// </summary>
/// <param name="Thesis">The thesis statement, or <see langword="null" /> when the draft had none.</param>
/// <param name="Areas">The published reading areas, one per track.</param>
/// <param name="Citations">The citations that bind statements to disclosed evidence.</param>
/// <param name="Evidence">The disclosed evidence quotes the citations address.</param>
/// <param name="Limitations">The publication limitations.</param>
/// <param name="OmissionSummaries">The totals of evidence publication did not quote.</param>
/// <param name="Assurances">The statements about what publication did not run or verify.</param>
internal sealed record ReadingModelResult(
    ReadingStatementResult? Thesis,
    IReadOnlyList<ReadingAreaResult> Areas,
    IReadOnlyList<ReadingCitationResult> Citations,
    IReadOnlyList<ReadingEvidenceResult> Evidence,
    IReadOnlyList<ReadingLimitationResult> Limitations,
    IReadOnlyList<ReadingOmissionSummaryResult> OmissionSummaries,
    IReadOnlyList<ReadingAssuranceResult> Assurances);
