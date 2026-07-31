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
    public async Task RunAsync(
        Guid runId,
        AnalysisCheckSelection checks,
        CancellationToken userCancellationToken,
        CancellationToken shutdownToken)
    {
        var plan = BuildPlan();
        var planResult = await store.EstablishStepPlanAsync(runId, plan, CancellationToken.None);
        if (planResult.IsFailure)
        {
            return;
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
                    return;
                }

                currentState = nextState;
            }

            var outcome = await this.RunStepAsync(runId, entry, checks);
            if (outcome.State is AnalysisRunStepState.SucceededWithLimitations)
            {
                limitationCount++;
            }
        }

        var terminalTransition = await store.TransitionStageAsync(
            runId,
            AnalysisRunState.Checking,
            AnalysisRunState.Persisting,
            this.Now(),
            CancellationToken.None);
        if (terminalTransition.IsFailure)
        {
            return;
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
        await store.CommitTerminalAsync(runId, terminal, CancellationToken.None);
        logger.LogInformation(
            "Analysis run {RunId} reached terminal {TerminalKind} with {LimitationCount} limitation(s).",
            runId,
            terminal.Kind,
            limitationCount);
    }

    private static IReadOnlyList<AnalysisRunStepPlanEntry> BuildPlan() =>
    [
        new(AnalysisStepId.Capture, "engine", "lifecycle", 0, AnalysisStage.Capturing),
        new(AnalysisStepId.Discover, "engine", "lifecycle", 1, AnalysisStage.Discovering),
        new(AnalysisStepId.Collect, "engine", "lifecycle", 2, AnalysisStage.Collecting),
        new(AnalysisStepId.CheckBuild, "engine", "build", 3, AnalysisStage.Checking),
        new(AnalysisStepId.CheckTests, "engine", "tests", 4, AnalysisStage.Checking),
    ];

    private async Task<AnalysisRunStepOutcome> RunStepAsync(Guid runId, AnalysisRunStepPlanEntry entry, AnalysisCheckSelection checks)
    {
        var beginResult = await store.BeginStepAsync(runId, entry.StepId, this.Now(), CancellationToken.None);
        if (beginResult.IsFailure)
        {
            return new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Failed, AnalysisFailureCode.UnexpectedFailure);
        }

        var outcome = entry.StepId switch
        {
            AnalysisStepId.CheckBuild => Skip(entry.StepId, checks.Build),
            AnalysisStepId.CheckTests => Skip(entry.StepId, checks.Tests),
            _ => new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Succeeded, null),
        };
        var finishResult = await store.FinishStepAsync(runId, outcome, this.Now(), CancellationToken.None);
        return finishResult.IsFailure
            ? new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Failed, AnalysisFailureCode.UnexpectedFailure)
            : outcome;

        static AnalysisRunStepOutcome Skip(string stepId, bool selected) => selected
            ? new AnalysisRunStepOutcome(stepId, AnalysisRunStepState.SucceededWithLimitations, AnalysisLimitationReason.CapabilityUnavailable)
            : new AnalysisRunStepOutcome(stepId, AnalysisRunStepState.Skipped, AnalysisLimitationReason.Disabled);
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
        AnalysisStage.Checking => AnalysisRunState.Checking,
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };
}
