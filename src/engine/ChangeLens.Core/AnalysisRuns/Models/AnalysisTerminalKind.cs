namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Defines the strict terminal outcome kind for a completed analysis run.
/// </summary>
public enum AnalysisTerminalKind
{
    /// <summary>The run completed with no limitations.</summary>
    Completed,

    /// <summary>The run completed with one or more limitations.</summary>
    CompletedWithLimitations,

    /// <summary>The run stopped after a durable cancellation request.</summary>
    Cancelled,

    /// <summary>The run stopped after an unexpected failure.</summary>
    Failed,
}
