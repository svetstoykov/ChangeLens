namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides the controlled reasons a deterministic check step is skipped.
/// </summary>
public static class AnalysisLimitationReason
{
    /// <summary>The check was not selected. Not a limitation.</summary>
    public const string Disabled = "disabled";

    /// <summary>The check was selected but its capability is not implemented in this gate. A limitation.</summary>
    public const string CapabilityUnavailable = "capabilityUnavailable";
}
