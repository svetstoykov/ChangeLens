namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides the controlled reasons recorded against an analysis step whose outcome reduced the evidence a run collected.
/// </summary>
public static class AnalysisLimitationReason
{
    /// <summary>The capability the step serves is unavailable, so the run completes with a limitation.</summary>
    public const string CapabilityUnavailable = "capabilityUnavailable";

    /// <summary>Uncommitted work existed in the repository and was not part of the captured snapshot.</summary>
    public const string UncommittedWorkExcluded = "uncommittedWorkExcluded";

    /// <summary>The review failed or its contribution did not fit the response budget, so the run publishes the explanation alone.</summary>
    public const string ReviewUnavailable = "reviewUnavailable";
}
