namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides the stable step identifiers for the analysis pipeline plan.
/// </summary>
public static class AnalysisStepId
{
    /// <summary>The step that captures and commits the frozen snapshot manifest.</summary>
    public const string Capture = "analysis.lifecycle.capture";

    /// <summary>The step that analyzes, ranks, graphs, discloses, and binds the frozen evidence.</summary>
    public const string Discover = "analysis.lifecycle.discover";

    /// <summary>The step that curates, validates, and publishes one reading model.</summary>
    public const string Collect = "analysis.lifecycle.collect";

    /// <summary>The lifecycle step that derives the terminal summary.</summary>
    public const string Persist = "analysis.lifecycle.persist";
}
