using ChangeLens.Core.Analysis.DraftValidation.Models;

namespace ChangeLens.Engine.Analysis.Pipeline.Models;

/// <summary>
///     Represents the immutable handoff produced when curation, review, and draft validation complete.
/// </summary>
/// <param name="Validation">The validated curator draft and its removals. Cannot be <see langword="null" />.</param>
/// <param name="Review">The review contribution for publication. Cannot be <see langword="null" />.</param>
internal sealed record CurationStepOutput(DraftValidationOutcome Validation, ReviewContribution Review);
