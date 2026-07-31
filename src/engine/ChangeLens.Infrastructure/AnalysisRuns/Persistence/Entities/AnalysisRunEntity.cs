using ChangeLens.Core.AnalysisRuns.Models;

namespace ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;

/// <summary>
///     Represents persisted durable state for one analysis run.
/// </summary>
internal sealed class AnalysisRunEntity
{
    /// <summary>
    ///     Gets or sets the run identifier.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    ///     Gets or sets the ChangeLens-generated repository identifier.
    /// </summary>
    public Guid RepositoryId { get; set; }

    /// <summary>
    ///     Gets or sets the repository display name captured at acceptance.
    /// </summary>
    public required string RepositoryDisplayName { get; set; }

    /// <summary>
    ///     Gets or sets the canonical absolute repository path captured at acceptance.
    /// </summary>
    public required string CanonicalRepositoryPath { get; set; }

    /// <summary>
    ///     Gets or sets the normalized key derived from the canonical repository path.
    /// </summary>
    public required string CanonicalRepositoryPathKey { get; set; }

    /// <summary>
    ///     Gets or sets the repository HEAD revision captured at acceptance.
    /// </summary>
    public required string HeadRevision { get; set; }

    /// <summary>
    ///     Gets or sets the accepted comparison target reference.
    /// </summary>
    public required string Target { get; set; }

    /// <summary>
    ///     Gets or sets the resolved comparison target revision captured at acceptance.
    /// </summary>
    public required string TargetRevision { get; set; }

    /// <summary>
    ///     Gets or sets the freshness token used to detect a stale acceptance.
    /// </summary>
    public required string FreshnessToken { get; set; }

    /// <summary>
    ///     Gets or sets the optional free-text change context supplied at acceptance, or <see langword="null" />
    ///     when none was supplied.
    /// </summary>
    public string? ChangeContext { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the build check is enabled for this run.
    /// </summary>
    public bool BuildEnabled { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the tests check is enabled for this run.
    /// </summary>
    public bool TestsEnabled { get; set; }

    /// <summary>
    ///     Gets or sets the identifier of the processor session that claimed this run.
    /// </summary>
    public Guid ProcessorSessionId { get; set; }

    /// <summary>
    ///     Gets or sets the durable lifecycle state.
    /// </summary>
    public AnalysisRunState State { get; set; }

    /// <summary>
    ///     Gets or sets the acceptance timestamp in UTC Unix milliseconds.
    /// </summary>
    public long RequestedAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the claim timestamp in UTC Unix milliseconds, or <see langword="null" /> before the run is
    ///     claimed.
    /// </summary>
    public long? CaptureStartedAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the capture-completion timestamp in UTC Unix milliseconds, or <see langword="null" />
    ///     before capture completes.
    /// </summary>
    public long? CapturedAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp in UTC Unix milliseconds at which the run entered its first analysis stage,
    ///     or <see langword="null" /> before that transition.
    /// </summary>
    public long? AnalysisStartedAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the terminal timestamp in UTC Unix milliseconds, or <see langword="null" /> while the run
    ///     is not yet terminal.
    /// </summary>
    public long? TerminalAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp in UTC Unix milliseconds at which the run was classified interrupted, or
    ///     <see langword="null" /> when the run was never interrupted.
    /// </summary>
    public long? InterruptedAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp in UTC Unix milliseconds at which cancellation was durably requested, or
    ///     <see langword="null" /> when cancellation was never requested.
    /// </summary>
    public long? CancellationRequestedAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the identifier of the captured evidence snapshot, or <see langword="null" /> before
    ///     capture completes.
    /// </summary>
    public string? SnapshotId { get; set; }

    /// <summary>
    ///     Gets or sets the recorded limitation count for a terminal run, or <see langword="null" /> while the run
    ///     is not yet terminal.
    /// </summary>
    public int? TerminalLimitationCount { get; set; }

    /// <summary>
    ///     Gets or sets the stable terminal failure code for a failed run, or <see langword="null" /> when the run
    ///     did not fail.
    /// </summary>
    public string? TerminalFailureCode { get; set; }

    /// <summary>
    ///     Gets or sets the stable interruption reason, or <see langword="null" /> when the run was never
    ///     interrupted.
    /// </summary>
    public string? InterruptionReason { get; set; }

    /// <summary>
    ///     Gets or sets the planned steps belonging to this run.
    /// </summary>
    public List<AnalysisRunStepEntity> Steps { get; set; } = [];
}
