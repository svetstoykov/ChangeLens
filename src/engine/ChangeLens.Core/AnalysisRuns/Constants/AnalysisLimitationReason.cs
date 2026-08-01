namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides the controlled reasons recorded against an analysis step whose outcome reduced the evidence a run collected.
/// </summary>
public static class AnalysisLimitationReason
{
    /// <summary>The capability the step serves is unavailable, so the run completes with a limitation.</summary>
    public const string CapabilityUnavailable = "capabilityUnavailable";
}
