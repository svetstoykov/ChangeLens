using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.AnalysisRuns.Services;

/// <summary>
///     Implements the gate 2.1 shallow deterministic analysis pipeline.
/// </summary>
internal sealed class ShallowAnalysisPipeline(
    IAnalysisRunStore store,
    ISnapshotCaptureService captureService,
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

            var outcome = await this.RunStepAsync(runId, entry, userCancellationToken);
            if (outcome.State is AnalysisRunStepState.Failed)
            {
                var failed = new AnalysisTerminalSummary(AnalysisTerminalKind.Failed, this.Now(), null,
                    outcome.Code ?? AnalysisFailureCode.UnexpectedFailure);
                await store.CommitTerminalAsync(runId, failed, CancellationToken.None);
                logger.LogWarning("Analysis run {RunId} failed at step {StepId} with {FailureCode}.", runId, entry.StepId, failed.FailureCode);
                return;
            }

            if (outcome.State is AnalysisRunStepState.Cancelled)
            {
                await this.CommitCancelledAsync(runId);
                return;
            }

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

    private async Task<AnalysisRunStepOutcome> RunStepAsync(
        Guid runId,
        AnalysisRunStepPlanEntry entry,
        CancellationToken userCancellationToken)
    {
        var beginResult = await store.BeginStepAsync(runId, entry.StepId, this.Now(), CancellationToken.None);
        if (beginResult.IsFailure)
        {
            return new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Failed, AnalysisFailureCode.UnexpectedFailure);
        }

        var outcome = entry.StepId == AnalysisStepId.Capture
            ? await this.ExecuteCaptureAsync(runId, userCancellationToken)
            : new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Succeeded, null);
        var finishResult = await store.FinishStepAsync(runId, outcome, this.Now(), CancellationToken.None);
        return finishResult.IsFailure
            ? new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Failed, AnalysisFailureCode.UnexpectedFailure)
            : outcome;
    }

    private async Task<AnalysisRunStepOutcome> ExecuteCaptureAsync(Guid runId, CancellationToken userCancellationToken)
    {
        var detailResult = await store.GetDetailAsync(runId, CancellationToken.None);
        if (detailResult.IsFailure)
        {
            throw new InvalidOperationException(
                "The analysis pipeline could not read the capture detail. Errors: " +
                string.Join(", ", detailResult.Errors.Select(error => error.Code)) + ".");
        }

        var captureResult = await captureService.CaptureAsync(detailResult.Data!, userCancellationToken);
        if (captureResult.IsFailure)
        {
            return new AnalysisRunStepOutcome(
                AnalysisStepId.Capture,
                AnalysisRunStepState.Failed,
                captureResult.Errors[0].Code ?? AnalysisFailureCode.CaptureFailed);
        }

        var capture = captureResult.Data!;
        var commitResult = await store.CommitCaptureAsync(runId, capture, this.Now(), CancellationToken.None);
        if (commitResult.IsFailure)
        {
            throw new InvalidOperationException(
                "The analysis pipeline could not commit the capture. Errors: " +
                string.Join(", ", commitResult.Errors.Select(error => error.Code)) + ".");
        }

        if (commitResult.Data)
        {
            return this.CaptureOutcome(capture);
        }

        var durableDetail = await store.GetDetailAsync(runId, CancellationToken.None);
        if (durableDetail.IsFailure)
        {
            throw new InvalidOperationException(
                "The analysis pipeline could not re-read the capture detail. Errors: " +
                string.Join(", ", durableDetail.Errors.Select(error => error.Code)) + ".");
        }

        var detail = durableDetail.Data!;
        if (detail.CancellationRequested && detail.Terminal is null)
        {
            return new AnalysisRunStepOutcome(AnalysisStepId.Capture, AnalysisRunStepState.Cancelled, null);
        }

        if (detail.SnapshotId is not null && StringComparer.Ordinal.Equals(detail.ManifestHash, capture.Manifest.ManifestHash))
        {
            return this.CaptureOutcome(capture);
        }

        if (detail.Terminal is not null || detail.State != AnalysisRunState.Capturing)
        {
            return new AnalysisRunStepOutcome(AnalysisStepId.Capture, AnalysisRunStepState.Cancelled, null);
        }

        throw new InvalidOperationException("The analysis pipeline observed unexpected durable capture state drift.");
    }

    private AnalysisRunStepOutcome CaptureOutcome(SnapshotCapture capture) =>
        capture.ExcludedUncommittedCounts.Total > 0
            ? new AnalysisRunStepOutcome(
                AnalysisStepId.Capture,
                AnalysisRunStepState.SucceededWithLimitations,
                AnalysisLimitationReason.UncommittedWorkExcluded)
            : new AnalysisRunStepOutcome(AnalysisStepId.Capture, AnalysisRunStepState.Succeeded, null);

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
