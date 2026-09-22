namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one publication limitation.
/// </summary>
/// <param name="Kind">The limitation kind wire value.</param>
/// <param name="Path">The exact resolved repository-relative path, or <see langword="null" /> when the limitation has none.</param>
/// <param name="Detail">The limitation detail.</param>
internal sealed record ReadingLimitationResult(string Kind, string? Path, string Detail);
