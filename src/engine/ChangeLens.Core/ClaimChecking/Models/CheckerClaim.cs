namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Represents one complete claim and its isolated evidence quotes for a checker.
/// </summary>
/// <param name="ClaimId">The stable published claim identifier.</param>
/// <param name="Type">The claim type: summary, step, purpose, or relationship.</param>
/// <param name="Text">The complete claim text.</param>
/// <param name="Kind">The relationship kind, or <see langword="null" /> for statements.</param>
/// <param name="From">The formatted relationship source participant, or <see langword="null" />.</param>
/// <param name="To">The formatted relationship target participant, or <see langword="null" />.</param>
/// <param name="Quotes">The binder-held quotes supplied with the claim.</param>
public sealed record CheckerClaim(
    string ClaimId,
    string Type,
    string Text,
    string? Kind,
    string? From,
    string? To,
    IReadOnlyList<CheckerQuote> Quotes);
