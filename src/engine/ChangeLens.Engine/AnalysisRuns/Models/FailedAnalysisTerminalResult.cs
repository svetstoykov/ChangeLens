namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>Represents a run that stopped after an unexpected failure.</summary>
/// <param name="TerminalAt">The Unix timestamp in milliseconds when the run became terminal.</param>
/// <param name="FailureCode">The stable code identifying the failure.</param>
internal sealed record FailedAnalysisTerminalResult(long TerminalAt, string FailureCode) : AnalysisTerminalResult(TerminalAt);
