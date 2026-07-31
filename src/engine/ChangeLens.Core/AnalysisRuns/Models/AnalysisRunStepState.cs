namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Defines the durable state of one planned analysis run step.
/// </summary>
public enum AnalysisRunStepState
{
    /// <summary>The step has not started.</summary>
    Pending,

    /// <summary>The step is executing.</summary>
    Running,

    /// <summary>The step completed with no limitations.</summary>
    Succeeded,

    /// <summary>The step completed with a recorded limitation.</summary>
    SucceededWithLimitations,

    /// <summary>The step failed.</summary>
    Failed,

    /// <summary>The step did not run, disabled or unavailable.</summary>
    Skipped,

    /// <summary>The step stopped after a durable cancellation request.</summary>
    Cancelled,

    /// <summary>The step exceeded its allowed time.</summary>
    TimedOut,
}
