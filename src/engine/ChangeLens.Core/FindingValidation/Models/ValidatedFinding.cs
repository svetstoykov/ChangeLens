using ChangeLens.Core.Review.Models;

namespace ChangeLens.Core.FindingValidation.Models;

/// <summary>
///     Represents a reviewer finding that passed mechanical publication checks.
/// </summary>
/// <param name="Finding">The reviewer finding that passed validation.</param>
/// <param name="FocusRange">The absolute source lines matched by the finding anchor.</param>
/// <param name="DraftPosition">The zero-based position of the finding in the reviewer draft.</param>
public sealed record ValidatedFinding(ReviewerFinding Finding, FindingFocusRange FocusRange, int DraftPosition);
