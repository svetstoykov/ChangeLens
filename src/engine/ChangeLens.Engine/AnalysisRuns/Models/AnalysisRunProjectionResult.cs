namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the current-state analysis run projection shared by poll and active-lookup results.
/// </summary>
/// <param name="RunId">The identifier assigned to the run.</param>
/// <param name="State">The current protocol state name.</param>
/// <param name="Repository">The repository identity used by the run.</param>
/// <param name="Comparison">The accepted comparison identity.</param>
/// <param name="RequestedAt">The Unix timestamp in milliseconds when the run was requested.</param>
/// <param name="CaptureStartedAt">The capture start timestamp, or <see langword="null" /> when it has not started.</param>
/// <param name="CapturedAt">The capture completion timestamp, or <see langword="null" /> when it has not completed.</param>
/// <param name="SnapshotId">The snapshot identifier, or <see langword="null" /> when none exists.</param>
/// <param name="CancellationRequested">Whether durable cancellation has been requested.</param>
/// <param name="Facts">The bounded discovered-fact summaries.</param>
/// <param name="Terminal">The terminal outcome, or <see langword="null" /> while the run is active.</param>
/// <param name="InterruptedAt">The interruption timestamp, or <see langword="null" /> when the run was not interrupted.</param>
/// <param name="InterruptionReason">The interruption reason, or <see langword="null" /> when the run was not interrupted.</param>
internal sealed record AnalysisRunProjectionResult(
    string RunId,
    string State,
    AnalysisRepositoryResult Repository,
    AnalysisComparisonResult Comparison,
    long RequestedAt,
    long? CaptureStartedAt,
    long? CapturedAt,
    string? SnapshotId,
    bool CancellationRequested,
    IReadOnlyList<AnalysisFactResult> Facts,
    AnalysisTerminalResult? Terminal,
    long? InterruptedAt,
    string? InterruptionReason);
