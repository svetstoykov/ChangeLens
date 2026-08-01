namespace ChangeLens.Engine.AnalysisRuns.Constants;

/// <summary>
///     Provides fixed action names for analysis protocol requests.
/// </summary>
internal static class AnalysisActionConstants
{
    /// <summary>Defines the action that starts an analysis run.</summary>
    internal const string StartAction = "analysis.start";

    /// <summary>Defines the action that looks up the active run for a repository.</summary>
    internal const string GetActiveAction = "analysis.getActive";

    /// <summary>Defines the action that polls one analysis run.</summary>
    internal const string PollRunAction = "analysis.pollRun";

    /// <summary>Defines the action that requests cancellation of one analysis run.</summary>
    internal const string CancelAction = "analysis.cancel";
}
