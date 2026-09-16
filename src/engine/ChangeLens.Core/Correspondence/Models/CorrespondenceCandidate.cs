namespace ChangeLens.Core.Correspondence.Models;

/// <summary>
///     Represents one unchanged file ranked for attention because it corresponds to the change.
/// </summary>
/// <remarks>
///     <para>
///         The score counts every signal in <see cref="Signals" />. <see cref="Reasons" /> is the capped prefix intended
///         for display, so the displayed reasons can be shorter than the evidence that earned the rank.
///     </para>
///     <para>
///         A score is attention, not a proven dependency.
///     </para>
/// </remarks>
/// <param name="Rank">The one-based position in the returned candidate list.</param>
/// <param name="Path">The repository-relative path in the captured HEAD tree. Cannot be <see langword="null" />.</param>
/// <param name="ObjectId">The captured blob object identifier. Cannot be <see langword="null" />.</param>
/// <param name="Score">The summed signal contribution, rounded to three decimal places.</param>
/// <param name="DominantSignal">The signal family with the largest summed contribution, excluding cross-language boosts.</param>
/// <param name="MatchedChangedPaths">The ordinal-sorted changed paths this candidate matched. Cannot be <see langword="null" />.</param>
/// <param name="Signals">
///     Every signal, keyed and co-change signals first by descending contribution, cross-language boosts last. Cannot be
///     <see langword="null" />.
/// </param>
/// <param name="Reasons">The leading signals kept under the per-candidate reason cap. Cannot be <see langword="null" />.</param>
public sealed record CorrespondenceCandidate(
    int Rank,
    string Path,
    string ObjectId,
    double Score,
    CorrespondenceSignalKind DominantSignal,
    IReadOnlyList<string> MatchedChangedPaths,
    IReadOnlyList<CorrespondenceSignal> Signals,
    IReadOnlyList<CorrespondenceSignal> Reasons)
{
    /// <summary>
    ///     Gets the number of signals left out of <see cref="Reasons" /> by the reason cap.
    /// </summary>
    public int OmittedReasonCount => this.Signals.Count - this.Reasons.Count;
}
