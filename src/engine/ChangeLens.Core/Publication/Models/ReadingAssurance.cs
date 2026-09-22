namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents one assurance attached to the reading model.</summary>
/// <param name="Kind">The assurance category.</param>
/// <param name="Detail">The human-readable detail.</param>
/// <param name="ClaimId">The affected claim identifier, when applicable.</param>
public sealed record ReadingAssurance(ReadingAssuranceKind Kind, string Detail, string? ClaimId = null);
