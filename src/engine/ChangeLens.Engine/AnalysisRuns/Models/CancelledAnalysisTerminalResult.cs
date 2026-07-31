namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>Represents a run that stopped after a durable cancellation request.</summary>
/// <param name="TerminalAt">The Unix timestamp in milliseconds when the run became terminal.</param>
internal sealed record CancelledAnalysisTerminalResult(long TerminalAt) : AnalysisTerminalResult(TerminalAt);
