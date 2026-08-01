using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.AnalysisRuns.Services;

/// <summary>
///     Implements the gate 2.1 shallow deterministic analysis pipeline.
/// </summary>
internal sealed class ShallowAnalysisPipeline(
    IAnalysisRunStore store,
    TimeProvider timeProvider,
    ILogger<ShallowAnalysisPipeline> logger) : IAnalysisPipeline
{
    /// <inheritdoc />
    public async Task RunAsync(Guid runId, CancellationToken userCancellationToken, CancellationToken shutdownToken)
    {
        var plan = BuildPlan();
        var planResult = await store.EstablishStepPlanAsync(runId, plan, CancellationToken.None);
        if (planResult.IsFailure)
        {
            logger.LogError("Analysis run {RunId} could not establish its step plan with errors {ErrorCodes}.", runId,
                planResult.Errors.Select(error => error.Code));

            throw new InvalidOperationException(
                "The analysis pipeline could not establish the step plan. Errors: " +
                string.Join(", ", planResult.Errors.Select(error => error.Code)) + ".");
        }

        var limitationCount = 0;
        var currentState = AnalysisRunState.Capturing;
        foreach (var entry in plan)
        {
            if (shutdownToken.IsCancellationRequested)
            {
                return;
            }

            if (userCancellationToken.IsCancellationRequested)
            {
                await this.CommitCancelledAsync(runId);
                return;
            }

            var nextState = this.StateFor(entry.Stage);
            if (nextState != currentState)
            {
                var transition = await store.TransitionStageAsync(runId, currentState, nextState, this.Now(), CancellationToken.None);
                if (transition.IsFailure)
                {
                    logger.LogError("Analysis run {RunId} could not transition from {CurrentState} to {NextState} with errors {ErrorCodes}.",
                        runId, currentState, nextState, transition.Errors.Select(error => error.Code));

                    throw new InvalidOperationException(
                        "The analysis pipeline could not transition the run stage. Errors: " +
                        string.Join(", ", transition.Errors.Select(error => error.Code)) + ".");
                }

                if (transition.Data != nextState)
                {
                    logger.LogWarning("Analysis run {RunId} drifted to {ObservedState} while transitioning from {CurrentState} to {NextState}.",
                        runId, transition.Data, currentState, nextState);
                    throw new InvalidOperationException(
                        $"The analysis pipeline observed an unexpected run state drift to {transition.Data}.");
                }

                currentState = nextState;
            }

            var outcome = await this.RunStepAsync(runId, entry);
            if (outcome.State is AnalysisRunStepState.SucceededWithLimitations)
            {
                limitationCount++;
            }
        }

        var terminalTransition = await store.TransitionStageAsync(runId, AnalysisRunState.Collecting, AnalysisRunState.Persisting, this.Now(),
            CancellationToken.None);
        if (terminalTransition.IsFailure)
        {
            logger.LogError("Analysis run {RunId} could not transition to Persisting with errors {ErrorCodes}.", runId,
                terminalTransition.Errors.Select(error => error.Code));
            throw new InvalidOperationException(
                "The analysis pipeline could not transition the run stage. Errors: " +
                string.Join(", ", terminalTransition.Errors.Select(error => error.Code)) + ".");
        }

        if (terminalTransition.Data != AnalysisRunState.Persisting)
        {
            logger.LogWarning("Analysis run {RunId} drifted to {ObservedState} while transitioning from Collecting to Persisting.", runId,
                terminalTransition.Data);
            throw new InvalidOperationException(
                $"The analysis pipeline observed an unexpected run state drift to {terminalTransition.Data}.");
        }

        if (userCancellationToken.IsCancellationRequested)
        {
            await this.CommitCancelledAsync(runId);
            return;
        }

        var terminal = new AnalysisTerminalSummary(
            limitationCount > 0 ? AnalysisTerminalKind.CompletedWithLimitations : AnalysisTerminalKind.Completed,
            this.Now(),
            limitationCount > 0 ? limitationCount : null,
            null);
        var commitResult = await store.CommitTerminalAsync(runId, terminal, CancellationToken.None);
        if (commitResult.IsFailure)
        {
            logger.LogError("Analysis run {RunId} could not commit terminal {TerminalKind} with errors {ErrorCodes}.", runId, terminal.Kind,
                commitResult.Errors.Select(error => error.Code));
            throw new InvalidOperationException(
                "The analysis pipeline could not commit the terminal outcome. Errors: " +
                string.Join(", ", commitResult.Errors.Select(error => error.Code)) + ".");
        }

        if (!commitResult.Data)
        {
            logger.LogWarning("Analysis run {RunId} was already terminal when {TerminalKind} was observed.", runId, terminal.Kind);
            return;
        }

        logger.LogInformation("Analysis run {RunId} reached terminal {TerminalKind} with {LimitationCount} limitation(s).", runId, terminal.Kind,
            limitationCount);
    }

    private static IReadOnlyList<AnalysisRunStepPlanEntry> BuildPlan() =>
    [
        new(AnalysisStepId.Capture, "engine", "lifecycle", 0, AnalysisStage.Capturing),
        new(AnalysisStepId.Discover, "engine", "lifecycle", 1, AnalysisStage.Discovering),
        new(AnalysisStepId.Collect, "engine", "lifecycle", 2, AnalysisStage.Collecting),
    ];

    private async Task<AnalysisRunStepOutcome> RunStepAsync(Guid runId, AnalysisRunStepPlanEntry entry)
    {
        var beginResult = await store.BeginStepAsync(runId, entry.StepId, this.Now(), CancellationToken.None);
        if (beginResult.IsFailure)
        {
            return new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Failed, AnalysisFailureCode.UnexpectedFailure);
        }

        var outcome = new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Succeeded, null);
        var finishResult = await store.FinishStepAsync(runId, outcome, this.Now(), CancellationToken.None);
        return finishResult.IsFailure
            ? new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Failed, AnalysisFailureCode.UnexpectedFailure)
            : outcome;
    }

    private async Task CommitCancelledAsync(Guid runId)
    {
        var terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Cancelled, this.Now(), null, null);
        await store.CommitTerminalAsync(runId, terminal, CancellationToken.None);
        logger.LogInformation("Analysis run {RunId} committed Cancelled after observing durable cancellation.", runId);
    }

    private long Now() => timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

    private AnalysisRunState StateFor(AnalysisStage stage) => stage switch
    {
        AnalysisStage.Capturing => AnalysisRunState.Capturing,
        AnalysisStage.Discovering => AnalysisRunState.Discovering,
        AnalysisStage.Collecting => AnalysisRunState.Collecting,
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };
}
