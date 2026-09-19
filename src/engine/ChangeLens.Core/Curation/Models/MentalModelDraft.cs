namespace ChangeLens.Core.Curation.Models;

/// <summary>
///     Represents the complete mental-model draft returned by the curator.
/// </summary>
/// <param name="Thesis">The evidence-bound thesis.</param>
/// <param name="Tracks">The ordered mental-model tracks.</param>
/// <param name="DroppedNodeIds">The binder node identifiers the curator marked as unused.</param>
public sealed record MentalModelDraft(
    BoundStatement Thesis,
    IReadOnlyList<DraftTrack> Tracks,
    IReadOnlyList<string> DroppedNodeIds);
