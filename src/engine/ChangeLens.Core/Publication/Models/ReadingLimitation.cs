namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents one exact path limitation.</summary>
/// <param name="Kind">The limitation category.</param>
/// <param name="Path">The exact path resolved from the six publication inputs, or <see langword="null" /> for a comparison-wide limitation.</param>
/// <param name="Detail">The human-readable detail.</param>
public sealed record ReadingLimitation(ReadingLimitationKind Kind, string? Path, string Detail);
