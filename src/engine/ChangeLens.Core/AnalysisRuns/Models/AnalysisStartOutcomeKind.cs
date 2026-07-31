namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Defines the outcome of an analysis-start acceptance attempt.
/// </summary>
public enum AnalysisStartOutcomeKind
{
    /// <summary>The durable pending run was created.</summary>
    Accepted,

    /// <summary>The comparison moved since acceptance validation; no run was created.</summary>
    RejectedStale,

    /// <summary>A run is already active for the canonical repository; no run was created.</summary>
    RejectedActive,
}
