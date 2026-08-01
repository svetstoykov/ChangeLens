namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>Represents a run that completed with no limitations.</summary>
/// <param name="TerminalAt">The Unix timestamp in milliseconds when the run became terminal.</param>
internal sealed record CompletedAnalysisTerminalResult(long TerminalAt) : AnalysisTerminalResult(TerminalAt);
