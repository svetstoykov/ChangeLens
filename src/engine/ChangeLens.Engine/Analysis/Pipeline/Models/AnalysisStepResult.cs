using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.Analysis.Pipeline.Models;

/// <summary>
///     Represents the outcome of one pipeline step and, when the step completed, the typed output it hands off to the
///     next step.
/// </summary>
/// <typeparam name="TOutput">The typed output produced when the step completes.</typeparam>
/// <param name="Outcome">The durable step outcome. Cannot be <see langword="null" />.</param>
/// <param name="Output">
///     The typed step output when the step completed, or <see langword="null" /> when the step stopped early.
/// </param>
internal sealed record AnalysisStepResult<TOutput>(AnalysisRunStepOutcome Outcome, TOutput? Output)
    where TOutput : class
{
    /// <summary>Creates a completed step result carrying <paramref name="output" />.</summary>
    /// <param name="outcome">The completed step outcome. Cannot be <see langword="null" />.</param>
    /// <param name="output">The typed step output. Cannot be <see langword="null" />.</param>
    /// <returns>A completed <see cref="AnalysisStepResult{TOutput}" />.</returns>
    internal static AnalysisStepResult<TOutput> Completed(AnalysisRunStepOutcome outcome, TOutput output) => new(outcome, output);

    /// <summary>Creates a stopped step result that preserves a prior non-completing outcome without output.</summary>
    /// <param name="outcome">The non-completing step outcome. Cannot be <see langword="null" />.</param>
    /// <returns>A stopped <see cref="AnalysisStepResult{TOutput}" />.</returns>
    internal static AnalysisStepResult<TOutput> Stopped(AnalysisRunStepOutcome outcome) => new(outcome, null);

    /// <summary>Creates a failed step result using the first error code from <paramref name="errors" />.</summary>
    /// <param name="stepId">The stable step identifier. Cannot be <see langword="null" />.</param>
    /// <param name="errors">The operation errors describing the failure. Cannot be <see langword="null" />.</param>
    /// <returns>A failed <see cref="AnalysisStepResult{TOutput}" />.</returns>
    internal static AnalysisStepResult<TOutput> Failed(string stepId, IReadOnlyList<OperationError> errors) =>
        new(
            new AnalysisRunStepOutcome(stepId, AnalysisRunStepState.Failed, errors.FirstOrDefault()?.Code ?? AnalysisFailureCode.UnexpectedFailure),
            null);

    /// <summary>Creates a failed step result with an explicit failure code.</summary>
    /// <param name="stepId">The stable step identifier. Cannot be <see langword="null" />.</param>
    /// <param name="failureCode">The stable failure code. Cannot be <see langword="null" />.</param>
    /// <returns>A failed <see cref="AnalysisStepResult{TOutput}" />.</returns>
    internal static AnalysisStepResult<TOutput> Failed(string stepId, string failureCode) =>
        new(new AnalysisRunStepOutcome(stepId, AnalysisRunStepState.Failed, failureCode), null);

    /// <summary>Creates a cancelled step result for <paramref name="stepId" />.</summary>
    /// <param name="stepId">The stable step identifier. Cannot be <see langword="null" />.</param>
    /// <returns>A cancelled <see cref="AnalysisStepResult{TOutput}" />.</returns>
    internal static AnalysisStepResult<TOutput> Cancelled(string stepId) =>
        new(new AnalysisRunStepOutcome(stepId, AnalysisRunStepState.Cancelled, null), null);
}
