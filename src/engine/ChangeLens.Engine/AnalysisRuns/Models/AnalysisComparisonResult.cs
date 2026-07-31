namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the immutable accepted comparison identity in an analysis projection.
/// </summary>
/// <param name="Target">The exact comparison target reference.</param>
/// <param name="TargetRevision">The revision resolved for the comparison target.</param>
/// <param name="FreshnessToken">The lowercase SHA-256 token for the accepted comparison.</param>
internal sealed record AnalysisComparisonResult(string Target, string TargetRevision, string FreshnessToken);
