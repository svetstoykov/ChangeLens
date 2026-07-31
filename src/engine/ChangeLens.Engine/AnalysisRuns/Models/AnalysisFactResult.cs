namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents one bounded summary of discovered facts.
/// </summary>
/// <param name="Kind">The stable kind of fact.</param>
/// <param name="Count">The number of facts represented.</param>
/// <param name="Detail">Optional detail about the facts.</param>
internal sealed record AnalysisFactResult(string Kind, int Count, string? Detail);
