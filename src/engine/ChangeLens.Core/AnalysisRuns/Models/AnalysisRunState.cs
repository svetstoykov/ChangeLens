namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Defines the durable lifecycle state of an analysis run.
/// </summary>
public enum AnalysisRunState
{
    /// <summary>The run is durably accepted and awaiting processor claim.</summary>
    PendingCapture,

    /// <summary>The processor has claimed the run and is validating the accepted context.</summary>
    Capturing,

    /// <summary>The run is validating the fixed skeleton plan.</summary>
    Discovering,

    /// <summary>The run is completing an empty evidence pass.</summary>
    Collecting,

    /// <summary>The run is validating accumulated step outcomes and deriving the terminal summary.</summary>
    Persisting,

    /// <summary>The run completed with no limitations.</summary>
    Completed,

    /// <summary>The run completed with one or more recorded limitations.</summary>
    CompletedWithLimitations,

    /// <summary>The run stopped after a durable cancellation request.</summary>
    Cancelled,

    /// <summary>The run stopped after an unexpected pipeline failure.</summary>
    Failed,

    /// <summary>An earlier processor session left the run non-terminal; startup recovery classified it.</summary>
    Interrupted,
}
