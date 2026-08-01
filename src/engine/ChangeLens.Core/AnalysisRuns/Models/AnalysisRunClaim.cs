namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents one run claimed by the processor for execution.
/// </summary>
/// <param name="RunId">The claimed run identifier.</param>
public sealed record AnalysisRunClaim(Guid RunId);
