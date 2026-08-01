namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides the stable interruption reasons recorded for a run classified as
///     <see cref="Models.AnalysisRunState.Interrupted" />.
/// </summary>
public static class AnalysisInterruptionReason
{
    /// <summary>The engine process stopped while the run was active in an earlier processor session.</summary>
    public const string EngineStopped = "engineStopped";
}
