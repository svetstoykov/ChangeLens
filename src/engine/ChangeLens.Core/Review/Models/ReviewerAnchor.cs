namespace ChangeLens.Core.Review.Models;

/// <summary>
///     Represents the source quote that focuses a reviewer finding.
/// </summary>
/// <param name="NodeId">The binder evidence node containing the quote.</param>
/// <param name="Lines">The whole consecutive source lines copied from the quote.</param>
public sealed record ReviewerAnchor(string NodeId, string Lines);
