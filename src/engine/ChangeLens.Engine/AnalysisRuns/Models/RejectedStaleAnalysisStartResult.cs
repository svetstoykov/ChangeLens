namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>Represents rejection because the comparison moved since acceptance validation.</summary>
internal sealed record RejectedStaleAnalysisStartResult : AnalysisStartResult;
