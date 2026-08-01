namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Defines the fixed semantic stage order presented for an active analysis run.
/// </summary>
public enum AnalysisStage
{
    /// <summary>Validating the durable accepted run context.</summary>
    Capturing,

    /// <summary>Validating the fixed skeleton plan.</summary>
    Discovering,

    /// <summary>Completing an empty evidence pass.</summary>
    Collecting,

    /// <summary>Validating accumulated step outcomes and deriving the terminal summary.</summary>
    Persisting,
}
