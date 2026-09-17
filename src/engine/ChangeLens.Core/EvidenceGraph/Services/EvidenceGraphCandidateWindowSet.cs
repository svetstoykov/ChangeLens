using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.EvidenceGraph.Services;

/// <summary>
///     Represents the candidate quote windows and match-line contributions collected in one pass.
/// </summary>
/// <param name="ByIndex">The windows for each candidate, keyed by candidate rank position. Cannot be <see langword="null" />.</param>
/// <param name="AllWindows">Every candidate window in candidate rank order. Cannot be <see langword="null" />.</param>
/// <param name="ContributionsByIndex">
///     The summed match-line contributions for each candidate, keyed by candidate rank position. Cannot be
///     <see langword="null" />.
/// </param>
/// <param name="WithoutQuotableLine">The number of readable candidates with no match line to quote.</param>
/// <param name="Failure">The first frozen-read failure, or <see langword="null" /> when every read succeeded.</param>
internal sealed record CandidateWindowSet(
    Dictionary<int, List<EvidenceGraphWindow>> ByIndex,
    List<EvidenceGraphWindow> AllWindows,
    Dictionary<int, Dictionary<int, double>> ContributionsByIndex,
    int WithoutQuotableLine,
    Result? Failure);
