using ChangeLens.Core.EvidenceBinder.Models;

namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents the transferable published reading model.</summary>
/// <param name="Comparison">The comparison identity.</param>
/// <param name="Thesis">The published thesis, or <see langword="null" />.</param>
/// <param name="Areas">The published mental-model areas.</param>
/// <param name="Citations">The claim-addressed citations.</param>
/// <param name="Evidence">The distinct binder-held evidence nodes used by citations.</param>
/// <param name="Limitations">The exact navigable limitations.</param>
/// <param name="OmissionSummaries">The full-count omission summaries.</param>
/// <param name="Assurances">The publication assurances.</param>
public sealed record ReadingModel(
    BinderComparison Comparison,
    ReadingStatement? Thesis,
    IReadOnlyList<ReadingArea> Areas,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<ReadingEvidence> Evidence,
    IReadOnlyList<ReadingLimitation> Limitations,
    IReadOnlyList<ReadingOmissionSummary> OmissionSummaries,
    IReadOnlyList<ReadingAssurance> Assurances);
