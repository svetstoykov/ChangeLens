using ChangeLens.Core.MentalModels.Models;

namespace ChangeLens.Core.Publication.Helpers;

/// <summary>Represents one published claim and the evidence it cites.</summary>
/// <param name="ClaimId">The stable published claim identifier.</param>
/// <param name="NodeIds">The evidence node identifiers cited by the claim.</param>
/// <param name="Focus">The resolved focus ranges, or an empty list.</param>
/// <param name="IsThesis">Whether the claim is the published thesis.</param>
/// <param name="IsParticipant">Whether the claim is a participant citation.</param>
internal sealed record Claimant(
    string ClaimId,
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<FocusRange> Focus,
    bool IsThesis,
    bool IsParticipant);
