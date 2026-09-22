using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.EvidenceGraph.Models;

namespace ChangeLens.Core.EvidenceFrontier.Models;

/// <summary>
///     Represents one ranked or graph-backed item omitted from the published evidence set.
/// </summary>
/// <param name="NodeId">The graph or bound node id, or <see langword="null" /> for a path-only candidate.</param>
/// <param name="Path">The repository-relative path associated with the item.</param>
/// <param name="Side">The comparison side associated with the item, or <see langword="null" /> for no side.</param>
/// <param name="CandidateRank">The candidate's one-based ranking position, or <see langword="null" />.</param>
/// <param name="CandidateScore">The candidate's total correspondence score, or <see langword="null" />.</param>
/// <param name="Salience">The graph or disclosed-node salience, or <see langword="null" />.</param>
/// <param name="Origins">The graph origins for the item. Cannot be <see langword="null" />.</param>
/// <param name="DominantSignal">The candidate's dominant correspondence signal, or <see langword="null" />.</param>
/// <param name="Reasons">The typed correspondence signals retained for the candidate. Cannot be <see langword="null" />.</param>
/// <param name="OmittedReasonCount">The number of candidate signals omitted by the ranking reason cap.</param>
/// <param name="OmissionKind">The stable frontier omission kind. Cannot be <see langword="null" />.</param>
/// <param name="OmissionReason">The human-readable reason for the omission. Cannot be <see langword="null" />.</param>
/// <param name="MatchedChangedPaths">The changed paths matched by the candidate. Cannot be <see langword="null" />.</param>
public sealed record FrontierEntry(
    string? NodeId,
    string Path,
    ChangeAnatomySide? Side,
    int? CandidateRank,
    double? CandidateScore,
    double? Salience,
    IReadOnlyList<EvidenceNodeOrigin> Origins,
    CorrespondenceSignalKind? DominantSignal,
    IReadOnlyList<CorrespondenceSignal> Reasons,
    int OmittedReasonCount,
    string OmissionKind,
    string OmissionReason,
    IReadOnlyList<string> MatchedChangedPaths);
