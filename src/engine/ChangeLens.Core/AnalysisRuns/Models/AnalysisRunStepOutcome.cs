namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents the durable outcome recorded for one planned analysis run step.
/// </summary>
/// <param name="StepId">The stable step identifier. Cannot be <see langword="null" />.</param>
/// <param name="State">The step's terminal or in-flight state.</param>
/// <param name="Code">
///     The controlled disabled, limitation, failure, cancellation, or timeout code
///     associated with <paramref name="State" />, or <see langword="null" /> when none
///     applies.
/// </param>
public sealed record AnalysisRunStepOutcome(string StepId, AnalysisRunStepState State, string? Code);
