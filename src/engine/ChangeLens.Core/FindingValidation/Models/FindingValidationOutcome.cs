using ChangeLens.Core.DraftValidation.Models;

namespace ChangeLens.Core.FindingValidation.Models;

/// <summary>
///     Represents the findings that passed validation and the findings withheld from publication.
/// </summary>
/// <param name="Findings">The validated findings in reviewer draft order.</param>
/// <param name="Removals">The ordered finding removal records.</param>
public sealed record FindingValidationOutcome(
    IReadOnlyList<ValidatedFinding> Findings,
    IReadOnlyList<ValidationRemoval> Removals)
{
    /// <summary>
    ///     Gets the number of findings withheld from publication.
    /// </summary>
    public int WithheldCount => this.Removals.Count;
}
