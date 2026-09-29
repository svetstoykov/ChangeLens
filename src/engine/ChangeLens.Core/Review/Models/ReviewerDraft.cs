namespace ChangeLens.Core.Review.Models;

/// <summary>
///     Represents the complete parsed reviewer reply.
/// </summary>
/// <param name="Findings">The findings returned by the reviewer, in draft order.</param>
public sealed record ReviewerDraft(IReadOnlyList<ReviewerFinding> Findings);
