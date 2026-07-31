namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents one deterministic planned step established for a claimed run.
/// </summary>
/// <param name="StepId">The stable step identifier. Cannot be <see langword="null" />.</param>
/// <param name="Producer">The stable producer identifier. Cannot be <see langword="null" />.</param>
/// <param name="Capability">The stable capability identifier. Cannot be <see langword="null" />.</param>
/// <param name="Order">The deterministic zero-based order within the run.</param>
/// <param name="Stage">The stage this step belongs to.</param>
public sealed record AnalysisRunStepPlanEntry(string StepId, string Producer, string Capability, int Order, AnalysisStage Stage);
