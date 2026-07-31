namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents the transport-independent current-state detail of one analysis run.
/// </summary>
/// <param name="RunId">The run identifier. Cannot be <see langword="null" />.</param>
/// <param name="State">The current durable lifecycle state.</param>
/// <param name="Repository">
///     The immutable accepted repository identity. Cannot be <see langword="null" />.
/// </param>
/// <param name="Comparison">
///     The immutable accepted comparison identity. Cannot be <see langword="null" />.
/// </param>
/// <param name="Checks">
///     The immutable accepted deterministic check selection. Cannot be
///     <see langword="null" />.
/// </param>
/// <param name="RequestedAtUnixMilliseconds">
///     When the pending run and repository lock committed.
/// </param>
/// <param name="CaptureStartedAtUnixMilliseconds">
///     When the processor claimed the run, otherwise <see langword="null" />.
/// </param>
/// <param name="CancellationRequested">Whether cancellation has been durably requested.</param>
/// <param name="Terminal">
///     The strict terminal summary, or <see langword="null" /> for an active or
///     interrupted run.
/// </param>
/// <param name="InterruptedAtUnixMilliseconds">
///     When startup recovery classified the row, otherwise <see langword="null" />.
/// </param>
/// <param name="InterruptionReason">
///     The controlled interruption reason, or <see langword="null" /> when not
///     interrupted.
/// </param>
public sealed record AnalysisRunDetail(
    Guid RunId,
    AnalysisRunState State,
    AnalysisRepositoryIdentity Repository,
    AnalysisComparisonIdentity Comparison,
    AnalysisCheckSelection Checks,
    long RequestedAtUnixMilliseconds,
    long? CaptureStartedAtUnixMilliseconds,
    bool CancellationRequested,
    AnalysisTerminalSummary? Terminal,
    long? InterruptedAtUnixMilliseconds,
    string? InterruptionReason);
