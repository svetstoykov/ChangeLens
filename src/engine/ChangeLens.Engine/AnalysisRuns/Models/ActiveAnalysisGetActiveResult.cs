namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>Represents an active run for the resolved canonical repository.</summary>
/// <param name="Run">The current summary of the active run.</param>
internal sealed record ActiveAnalysisGetActiveResult(AnalysisRunSummaryResult Run) : AnalysisGetActiveResult;
