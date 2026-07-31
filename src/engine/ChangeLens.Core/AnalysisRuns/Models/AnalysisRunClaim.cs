namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents one run claimed by the processor for execution.
/// </summary>
/// <param name="RunId">The claimed run identifier.</param>
/// <param name="Checks">The immutable accepted deterministic check selection. Cannot be <see langword="null" />.</param>
public sealed record AnalysisRunClaim(Guid RunId, AnalysisCheckSelection Checks);
