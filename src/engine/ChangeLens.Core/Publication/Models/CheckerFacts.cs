namespace ChangeLens.Core.Publication.Models;

/// <summary>Describes whether claim checking participated in publication.</summary>
/// <param name="Ran">Whether the checker was called.</param>
/// <param name="Failed">Whether the checker failed or returned an unreadable reply.</param>
public sealed record CheckerFacts(bool Ran, bool Failed);
