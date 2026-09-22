namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Represents the timestamps of one persisted analysis step row read directly from local state.
/// </summary>
internal sealed class AnalysisRunStepRow
{
    /// <summary>Gets or sets the stable step identifier.</summary>
    public string StepId { get; set; } = string.Empty;

    /// <summary>Gets or sets the step start timestamp, or <see langword="null" /> before the step starts.</summary>
    public long? StartedAtUnixMilliseconds { get; set; }

    /// <summary>Gets or sets the step finish timestamp, or <see langword="null" /> before the step finishes.</summary>
    public long? FinishedAtUnixMilliseconds { get; set; }
}
