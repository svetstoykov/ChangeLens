using ChangeLens.Core.AnalysisRuns.Models;

namespace ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;

/// <summary>
///     Represents persisted durable state for one planned analysis run step.
/// </summary>
internal sealed class AnalysisRunStepEntity
{
    /// <summary>
    ///     Gets or sets the identifier of the owning run.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    ///     Gets or sets the stable step identifier, unique within the owning run.
    /// </summary>
    public required string StepId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the component that produces this step's outcome.
    /// </summary>
    public required string Producer { get; set; }

    /// <summary>
    ///     Gets or sets the capability this step exercises.
    /// </summary>
    public required string Capability { get; set; }

    /// <summary>
    ///     Gets or sets the zero-based planned execution order within the owning run.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    ///     Gets or sets the semantic stage this step belongs to.
    /// </summary>
    public AnalysisStage Stage { get; set; }

    /// <summary>
    ///     Gets or sets the durable step state.
    /// </summary>
    public AnalysisRunStepState State { get; set; }

    /// <summary>
    ///     Gets or sets the start timestamp in UTC Unix milliseconds, or <see langword="null" /> before the step
    ///     starts.
    /// </summary>
    public long? StartedAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the finish timestamp in UTC Unix milliseconds, or <see langword="null" /> before the step
    ///     finishes.
    /// </summary>
    public long? FinishedAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the stable outcome code, or <see langword="null" /> when the step has no recorded code.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    ///     Gets or sets the owning run.
    /// </summary>
    public AnalysisRunEntity Run { get; set; } = null!;
}
