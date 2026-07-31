namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents the immutable deterministic check selection accepted for a run.
/// </summary>
/// <param name="Build">Whether the build check is selected.</param>
/// <param name="Tests">Whether the test check is selected. Requires <paramref name="Build" />.</param>
public sealed record AnalysisCheckSelection(bool Build, bool Tests);
