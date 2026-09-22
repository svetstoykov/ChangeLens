using ChangeLens.Core.Snapshots.Models;

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
/// <param name="ChangeContext">
///     The optional untrusted developer prose supplied at acceptance, or <see langword="null" /> when none was supplied.
/// </param>
/// <param name="RequestedAtUnixMilliseconds">
///     When the pending run and repository lock committed.
/// </param>
/// <param name="CaptureStartedAtUnixMilliseconds">
///     When the processor took the run, otherwise <see langword="null" />.
/// </param>
/// <param name="CapturedAtUnixMilliseconds">
///     When the committed snapshot manifest was cut, otherwise <see langword="null" /> before capture completes.
/// </param>
/// <param name="SnapshotId">
///     The identifier of the captured evidence snapshot, or <see langword="null" /> before capture completes.
/// </param>
/// <param name="ManifestHash">
///     The deterministic content hash of the captured manifest, or <see langword="null" /> before capture
///     completes.
/// </param>
/// <param name="CapturedChangedFileCount">
///     The number of manifest entries captured, or <see langword="null" /> before capture completes.
/// </param>
/// <param name="ExcludedUncommittedCounts">
///     The uncommitted lineage counts excluded from the manifest, or <see langword="null" /> before capture
///     completes.
/// </param>
/// <param name="CorrespondenceCandidateCount">
///     The number of unchanged files correspondence ranking returned, or <see langword="null" /> before ranking finishes.
/// </param>
/// <param name="DisclosedEvidenceNodeCount">
///     The number of evidence nodes context policy disclosed, or <see langword="null" /> before context policy finishes.
/// </param>
/// <param name="ValidationRemovalCount">
///     The number of draft items mechanical validation removed, or <see langword="null" /> before validation finishes.
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
    string? ChangeContext,
    long RequestedAtUnixMilliseconds,
    long? CaptureStartedAtUnixMilliseconds,
    long? CapturedAtUnixMilliseconds,
    Guid? SnapshotId,
    string? ManifestHash,
    int? CapturedChangedFileCount,
    ExcludedUncommittedCounts? ExcludedUncommittedCounts,
    int? CorrespondenceCandidateCount,
    int? DisclosedEvidenceNodeCount,
    int? ValidationRemovalCount,
    bool CancellationRequested,
    AnalysisTerminalSummary? Terminal,
    long? InterruptedAtUnixMilliseconds,
    string? InterruptionReason);
