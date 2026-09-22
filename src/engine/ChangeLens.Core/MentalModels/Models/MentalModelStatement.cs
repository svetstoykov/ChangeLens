namespace ChangeLens.Core.MentalModels.Models;

/// <summary>
///     Represents a published statement with its stable claim id and evidence focus.
/// </summary>
/// <param name="ClaimId">The stable claim identifier.</param>
/// <param name="Text">The published statement text.</param>
/// <param name="EvidenceNodeIds">The evidence nodes cited by the statement.</param>
/// <param name="Focus">The resolved checker focus ranges.</param>
public sealed record MentalModelStatement(
    string ClaimId,
    string Text,
    IReadOnlyList<string> EvidenceNodeIds,
    IReadOnlyList<FocusRange> Focus);
