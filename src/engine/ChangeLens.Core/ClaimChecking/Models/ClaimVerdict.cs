using ChangeLens.Core.MentalModels.Models;

namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Represents one raw checker verdict and its optional focus ranges.
/// </summary>
/// <param name="ClaimId">The claim identifier named by the checker.</param>
/// <param name="Kind">The checker verdict kind.</param>
/// <param name="CorrectedKind">The replacement relationship kind, or <see langword="null" />.</param>
/// <param name="Focus">The focus ranges returned by the checker.</param>
public sealed record ClaimVerdict(
    string ClaimId,
    ClaimVerdictKind Kind,
    string? CorrectedKind,
    IReadOnlyList<FocusRange> Focus);
