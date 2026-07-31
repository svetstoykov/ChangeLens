namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>Represents a durably created pending run.</summary>
/// <param name="RunId">The identifier assigned to the run.</param>
/// <param name="RequestedAt">The Unix timestamp in milliseconds when the run was requested.</param>
internal sealed record AcceptedAnalysisStartResult(string RunId, long RequestedAt) : AnalysisStartResult;
