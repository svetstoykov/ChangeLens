namespace ChangeLens.Core.Review.Models;

/// <summary>
///     Represents a parsed reviewer draft or a recorded parse failure with diagnostics.
/// </summary>
/// <param name="Draft">The parsed draft, or an empty draft when parsing failed.</param>
/// <param name="Diagnostics">The provider, size, token, and parse diagnostics.</param>
public sealed record ReviewerOutcome(ReviewerDraft Draft, ReviewerDiagnostics Diagnostics);
