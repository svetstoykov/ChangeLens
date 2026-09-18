namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Represents the quote windows selected within the node budget.
/// </summary>
/// <param name="ChangedWindows">The selected changed-file windows in selection order. Cannot be <see langword="null" />.</param>
/// <param name="CandidateWindows">
///     The selected candidate windows paired with their candidate rank position. Cannot be <see langword="null" />.
/// </param>
internal sealed record EvidenceGraphWindowSelection(
    IReadOnlyList<EvidenceGraphWindow> ChangedWindows,
    IReadOnlyList<(int CandidateIndex, EvidenceGraphWindow Window)> CandidateWindows);
