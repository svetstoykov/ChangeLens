using ChangeLens.Core.Curation.Models;

namespace ChangeLens.Core.DraftValidation.Models;

/// <summary>
///     Represents a mechanically validated curator draft and the validation measurements.
/// </summary>
/// <param name="Draft">The draft after invalid items and undisclosed citations are removed.</param>
/// <param name="Removals">The ordered items removed during validation.</param>
/// <param name="InvalidReferenceCount">The number of distinct undisclosed references found in inspected lists.</param>
/// <param name="InvalidKindCount">The number of inspected relationships with an unknown kind.</param>
/// <param name="UndeclaredNodeCount">The number of disclosed binder nodes neither cited nor dropped by the draft.</param>
/// <param name="UncitedEndpointCount">The number of relationships missing evidence for one or both endpoints.</param>
public sealed record DraftValidationOutcome(
    MentalModelDraft Draft,
    IReadOnlyList<ValidationRemoval> Removals,
    int InvalidReferenceCount,
    int InvalidKindCount,
    int UndeclaredNodeCount,
    int UncitedEndpointCount)
{
    /// <summary>
    ///     Gets an empty validation outcome.
    /// </summary>
    public static DraftValidationOutcome Empty => new(new MentalModelDraft(new BoundStatement(string.Empty, []), [], []), [], 0, 0, 0, 0);
}
