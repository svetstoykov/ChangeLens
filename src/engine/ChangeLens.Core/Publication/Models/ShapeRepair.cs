namespace ChangeLens.Core.Publication.Models;

/// <summary>Records a repaired track rendering shape.</summary>
/// <param name="TrackId">The affected track identifier.</param>
/// <param name="Declared">The declared shape.</param>
/// <param name="Published">The published shape.</param>
public sealed record ShapeRepair(string TrackId, string Declared, string Published);
