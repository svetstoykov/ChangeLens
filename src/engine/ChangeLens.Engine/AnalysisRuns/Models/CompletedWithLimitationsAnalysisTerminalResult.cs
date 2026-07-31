namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>Represents a run that completed with one or more limitations.</summary>
/// <param name="TerminalAt">The Unix timestamp in milliseconds when the run became terminal.</param>
/// <param name="LimitationCount">The number of limitations recorded for the run.</param>
internal sealed record CompletedWithLimitationsAnalysisTerminalResult(long TerminalAt, int LimitationCount)
    : AnalysisTerminalResult(TerminalAt);
