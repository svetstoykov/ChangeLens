using ChangeLens.Core.Analysis.ClaimChecking.Models;

namespace ChangeLens.Core.Analysis.ClaimChecking.Models;

/// <summary>
///     Represents screened checker verdicts and refused correction count.
/// </summary>
/// <param name="Accepted">The verdicts that remain eligible for publication.</param>
/// <param name="RefusedCorrectionCount">The number of corrections refused by deterministic rules.</param>
internal sealed record CorrectionScreen(
    IReadOnlyDictionary<string, ClaimVerdict> Accepted,
    int RefusedCorrectionCount);
