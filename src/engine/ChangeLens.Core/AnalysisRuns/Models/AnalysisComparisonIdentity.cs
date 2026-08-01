namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents the immutable comparison identity accepted for a run.
/// </summary>
/// <param name="Target">The exact selected comparison target reference. Cannot be <see langword="null" />.</param>
/// <param name="TargetRevision">The accepted target revision. Cannot be <see langword="null" />.</param>
/// <param name="FreshnessToken">The accepted freshness token. Cannot be <see langword="null" />.</param>
public sealed record AnalysisComparisonIdentity(string Target, string TargetRevision, string FreshnessToken);
