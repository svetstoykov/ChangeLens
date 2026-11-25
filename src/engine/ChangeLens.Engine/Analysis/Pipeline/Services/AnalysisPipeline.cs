using System.Diagnostics;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Engine.Analysis.Pipeline.Helpers;
using ChangeLens.Engine.Analysis.Pipeline.Interfaces;
using ChangeLens.Engine.Analysis.Pipeline.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Analysis.Pipeline.Services;

/// <summary>
///     Orchestrates the deterministic analysis pipeline: capture the frozen change, discover and bind its evidence, and
///     collect one curated, reviewed, validated, and published reading model.
/// </summary>
/// <remarks>
///     <para>
///         The collect step runs curation and optional review concurrently through
///         <see cref="IAnalysisCurationCoordinator" /> and publishes the resulting reading model through
///         <see cref="IAnalysisPublicationStep" />. The user cancellation token reaches every step call; the shutdown token
///         is observed only between steps so engine shutdown leaves the active row for startup recovery to interrupt.
///     </para>
///     <para>
///         Registered as a scoped service. The implementation may depend on services registered with any lifetime and
///         does not need to be thread-safe.
///     </para>
/// </remarks>
/// <param name="store">The durable analysis run store. Cannot be <see langword="null" />.</param>
/// <param name="captureStep">The capture step collaborator. Cannot be <see langword="null" />.</param>
/// <param name="discoverStep">The discover step collaborator. Cannot be <see langword="null" />.</param>
/// <param name="curationCoordinator">The curation coordinator collaborator. Cannot be <see langword="null" />.</param>
/// <param name="publicationStep">The publication step collaborator. Cannot be <see langword="null" />.</param>
/// <param name="timeProvider">The time provider used for step and terminal timestamps. Cannot be <see langword="null" />.</param>
/// <param name="logger">The pipeline logger. Cannot be <see langword="null" />.</param>
internal sealed class AnalysisPipeline(
    IAnalysisRunStore store,
    IAnalysisCaptureStep captureStep,
    IAnalysisDiscoverStep discoverStep,
    IAnalysisCurationCoordinator curationCoordinator,
    IAnalysisPublicationStep publicationStep,
    TimeProvider timeProvider,
    ILogger<AnalysisPipeline> logger) : IAnalysisPipeline
{
    /// <inheritdoc />
    public async Task RunAsync(Guid runId, CancellationToken userCancellationToken, CancellationToken shutdownToken)
    {
        var runStartedAt = Stopwatch.GetTimestamp();
        var plan = BuildPlan();
        AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.EstablishStepPlanAsync(runId, plan, CancellationToken.None), "establish the step plan");

        var currentState = AnalysisRunState.Capturing;
        var limitationCount = 0;

        var (captureOutput, captureLimited) = await this.ExecutePlannedStepAsync(
            runId, plan[0], currentState, () => captureStep.ExecuteAsync(runId, userCancellationToken),
            userCancellationToken, shutdownToken, runStartedAt);
        if (captureOutput is null)
        {
            return;
        }

        if (captureLimited)
        {
            limitationCount++;
        }

        currentState = StateFor(plan[0].Stage);

        var (discoverOutput, discoverLimited) = await this.ExecutePlannedStepAsync(
            runId, plan[1], currentState, () => discoverStep.ExecuteAsync(runId, captureOutput, userCancellationToken),
            userCancellationToken, shutdownToken, runStartedAt);
        if (discoverOutput is null)
        {
            return;
        }

        if (discoverLimited)
        {
            limitationCount++;
        }

        currentState = StateFor(plan[1].Stage);

        var (projection, collectLimited) = await this.ExecutePlannedStepAsync(
            runId,
            plan[2],
            currentState,
            async () =>
            {
                var curationResult = await curationCoordinator.ExecuteAsync(runId, discoverOutput.Binder, userCancellationToken);
                return curationResult.Output is null
                    ? AnalysisStepResult<AnalysisReadingProjection>.Stopped(curationResult.Outcome)
                    : await publicationStep.ExecuteAsync(runId, discoverOutput, curationResult.Output, userCancellationToken);
            },
            userCancellationToken,
            shutdownToken,
            runStartedAt);
        if (projection is null)
        {
            return;
        }

        if (collectLimited)
        {
            limitationCount++;
        }

        currentState = StateFor(plan[2].Stage);
        if (shutdownToken.IsCancellationRequested)
        {
            return;
        }

        await this.TransitionStageAsync(runId, currentState, AnalysisRunState.Persisting);
        if (userCancellationToken.IsCancellationRequested)
        {
            await this.CommitCancelledAsync(runId, runStartedAt);
            return;
        }

        var terminal = new AnalysisTerminalSummary(
            limitationCount > 0 ? AnalysisTerminalKind.CompletedWithLimitations : AnalysisTerminalKind.Completed,
            this.Now(), limitationCount > 0 ? limitationCount : null, null);
        if (!await this.CommitTerminalAsync(runId, terminal, projection))
        {
            return;
        }

        logger.LogInformation(
            "Analysis run {RunId} reached terminal {TerminalKind} with {LimitationCount} limitation(s) in {ElapsedMilliseconds:0.000} ms.",
            runId, terminal.Kind, limitationCount, Stopwatch.GetElapsedTime(runStartedAt).TotalMilliseconds);
    }

    private async Task<(TOutput? Output, bool HadLimitation)> ExecutePlannedStepAsync<TOutput>(
        Guid runId, AnalysisRunStepPlanEntry entry, AnalysisRunState currentState, Func<Task<AnalysisStepResult<TOutput>>> executeStep,
        CancellationToken userCancellationToken, CancellationToken shutdownToken, long runStartedAt)
        where TOutput : class
    {
        if (shutdownToken.IsCancellationRequested)
        {
            return (null, false);
        }

        if (userCancellationToken.IsCancellationRequested)
        {
            await this.CommitCancelledAsync(runId, runStartedAt);
            return (null, false);
        }

        var nextState = StateFor(entry.Stage);
        if (nextState != currentState)
        {
            await this.TransitionStageAsync(runId, currentState, nextState);
        }

        AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.BeginStepAsync(runId, entry.StepId, this.Now(), CancellationToken.None), $"begin step {entry.StepId}");

        AnalysisStepResult<TOutput> stepResult;
        try
        {
            stepResult = await executeStep();
        }
        catch (OperationCanceledException) when (userCancellationToken.IsCancellationRequested)
        {
            stepResult = AnalysisStepResult<TOutput>.Cancelled(entry.StepId);
        }

        var outcome = stepResult.Outcome;
        AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.FinishStepAsync(runId, outcome, this.Now(), CancellationToken.None), $"finish step {entry.StepId}");

        if (outcome.State is AnalysisRunStepState.Failed)
        {
            var failed = new AnalysisTerminalSummary(
                AnalysisTerminalKind.Failed, this.Now(), null, outcome.Code ?? AnalysisFailureCode.UnexpectedFailure);
            if (await this.CommitTerminalAsync(runId, failed, null))
            {
                logger.LogWarning(
                    "Analysis run {RunId} failed at step {StepId} with {FailureCode} after {ElapsedMilliseconds:0.000} ms.",
                    runId, entry.StepId, failed.FailureCode, Stopwatch.GetElapsedTime(runStartedAt).TotalMilliseconds);
            }

            return (null, false);
        }

        if (outcome.State is AnalysisRunStepState.Cancelled)
        {
            await this.CommitCancelledAsync(runId, runStartedAt);
            return (null, false);
        }

        var output = stepResult.Output
            ?? throw new InvalidOperationException($"The analysis pipeline completed step {entry.StepId} without producing an output.");
        return (output, outcome.State is AnalysisRunStepState.SucceededWithLimitations);
    }

    private async Task TransitionStageAsync(Guid runId, AnalysisRunState currentState, AnalysisRunState nextState)
    {
        var observedState = AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.TransitionStageAsync(runId, currentState, nextState, this.Now(), CancellationToken.None),
            $"transition the run stage from {currentState} to {nextState}");
        if (observedState != nextState)
        {
            throw new InvalidOperationException(
                $"The analysis pipeline observed an unexpected run state drift to {observedState} while transitioning from "
                + $"{currentState} to {nextState}.");
        }
    }

    private async Task CommitCancelledAsync(Guid runId, long runStartedAt)
    {
        var terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Cancelled, this.Now(), null, null);
        if (await this.CommitTerminalAsync(runId, terminal, null))
        {
            logger.LogInformation(
                "Analysis run {RunId} committed Cancelled after observing durable cancellation in {ElapsedMilliseconds:0.000} ms.",
                runId, Stopwatch.GetElapsedTime(runStartedAt).TotalMilliseconds);
        }
    }

    private async Task<bool> CommitTerminalAsync(Guid runId, AnalysisTerminalSummary terminal, AnalysisReadingProjection? projection)
    {
        var committed = AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.CommitTerminalAsync(runId, terminal, projection, CancellationToken.None), $"commit terminal {terminal.Kind}");
        if (!committed)
        {
            logger.LogWarning("Analysis run {RunId} was already terminal when {TerminalKind} was observed.", runId, terminal.Kind);
        }

        return committed;
    }

    private long Now() => timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

    private static IReadOnlyList<AnalysisRunStepPlanEntry> BuildPlan() =>
    [
        new(AnalysisStepId.Capture, "engine", "lifecycle", 0, AnalysisStage.Capturing),
        new(AnalysisStepId.Discover, "engine", "lifecycle", 1, AnalysisStage.Discovering),
        new(AnalysisStepId.Collect, "engine", "lifecycle", 2, AnalysisStage.Collecting),
    ];

    private static AnalysisRunState StateFor(AnalysisStage stage) => stage switch
    {
        AnalysisStage.Capturing => AnalysisRunState.Capturing,
        AnalysisStage.Discovering => AnalysisRunState.Discovering,
        AnalysisStage.Collecting => AnalysisRunState.Collecting,
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };
}
