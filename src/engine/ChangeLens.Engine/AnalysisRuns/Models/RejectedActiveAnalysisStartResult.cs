namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>Represents rejection because a run is already active for the canonical repository.</summary>
/// <param name="ActiveRunId">The identifier of the already active run.</param>
internal sealed record RejectedActiveAnalysisStartResult(string ActiveRunId) : AnalysisStartResult;
