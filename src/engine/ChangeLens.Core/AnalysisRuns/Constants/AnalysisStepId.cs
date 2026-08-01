namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides the stable step identifiers for the gate 2.1 shallow production plan.
/// </summary>
public static class AnalysisStepId
{
    /// <summary>The lifecycle step that validates the durable accepted run context.</summary>
    public const string Capture = "analysis.lifecycle.capture";

    /// <summary>The lifecycle step that validates the fixed skeleton plan.</summary>
    public const string Discover = "analysis.lifecycle.discover";

    /// <summary>The lifecycle step that completes an empty evidence pass.</summary>
    public const string Collect = "analysis.lifecycle.collect";

    /// <summary>The lifecycle step that derives the terminal summary.</summary>
    public const string Persist = "analysis.lifecycle.persist";
}
