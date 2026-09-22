namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one citation from a claim to a disclosed evidence node.
/// </summary>
/// <param name="ClaimId">The citing claim identifier.</param>
/// <param name="NodeId">The cited evidence node identifier.</param>
/// <param name="Side">The comparison side wire value.</param>
/// <param name="Path">The repository-relative path of the quoted file.</param>
/// <param name="ObjectId">The Git object identifier of the quoted blob.</param>
/// <param name="StartLine">The first absolute file line of the quote.</param>
/// <param name="EndLine">The last absolute file line of the quote.</param>
/// <param name="Focus">
///     The disjoint focus ranges within the quote, in order. An empty list addresses the whole quote.
/// </param>
/// <param name="Provenance">The provenance wire value.</param>
internal sealed record ReadingCitationResult(
    string ClaimId,
    string NodeId,
    string Side,
    string Path,
    string ObjectId,
    int StartLine,
    int EndLine,
    IReadOnlyList<ReadingFocusRangeResult> Focus,
    string Provenance);
