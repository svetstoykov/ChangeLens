using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ChangeAnatomy.Interfaces;
using ChangeLens.Core.ContextPolicy.Interfaces;
using ChangeLens.Core.Correspondence.Interfaces;
using ChangeLens.Core.Curation.Interfaces;
using ChangeLens.Core.DraftValidation.Interfaces;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Interfaces;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceGraph.Interfaces;
using ChangeLens.Core.Publication.Interfaces;
using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Engine.AnalysisRuns.Helpers;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.AnalysisRuns.Services;

/// <summary>
///     Implements the deterministic analysis pipeline: capture the frozen change, discover and bind its evidence, and
///     collect one curated, validated, and published reading model.
/// </summary>
/// <remarks>
///     The user cancellation token reaches every service call. The shutdown token is observed only between steps, so
///     engine shutdown leaves the active row for startup recovery to interrupt instead of reporting a user cancellation.
/// </remarks>
internal sealed class AnalysisPipeline(
    IAnalysisRunStore store,
    ISnapshotCaptureService captureService,
    IChangeAnatomyService anatomyService,
    ICorrespondenceRankingService rankingService,
    IEvidenceGraphService graphService,
    IContextPolicyService policyService,
    IFrozenGitTreeReaderFactory treeReaderFactory,
    IEvidenceBinderService binderService,
    ICuratorService curatorService,
    IDraftValidationService validationService,
    IPublicationService publicationService,
    IEngineProtocolSerializer protocolSerializer,
    TimeProvider timeProvider,
    ILogger<AnalysisPipeline> logger) : IAnalysisPipeline
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

        var run = new AnalysisPipelineRun();
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

            var outcome = await this.RunStepAsync(runId, entry, run, userCancellationToken);
            if (outcome.State is AnalysisRunStepState.Failed)
            {
                var failed = new AnalysisTerminalSummary(AnalysisTerminalKind.Failed, this.Now(), null,
                    outcome.Code ?? AnalysisFailureCode.UnexpectedFailure);
                await store.CommitTerminalAsync(runId, failed, null, CancellationToken.None);
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

        if (shutdownToken.IsCancellationRequested)
        {
            return;
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
        var commitResult = await store.CommitTerminalAsync(runId, terminal, run.Projection, CancellationToken.None);
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

    private static AnalysisRunStepOutcome Failed(string stepId, IReadOnlyList<OperationError> errors) =>
        new(stepId, AnalysisRunStepState.Failed, errors.FirstOrDefault()?.Code ?? AnalysisFailureCode.UnexpectedFailure);

    private static void EnsureRecorded(Result recordResult, string count)
    {
        if (recordResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"The analysis pipeline could not record the {count} count. Errors: " +
                string.Join(", ", recordResult.Errors.Select(error => error.Code)) + ".");
        }
    }

    private async Task<AnalysisRunStepOutcome> RunStepAsync(
        Guid runId,
        AnalysisRunStepPlanEntry entry,
        AnalysisPipelineRun run,
        CancellationToken userCancellationToken)
    {
        var beginResult = await store.BeginStepAsync(runId, entry.StepId, this.Now(), CancellationToken.None);
        if (beginResult.IsFailure)
        {
            return new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Failed, AnalysisFailureCode.UnexpectedFailure);
        }

        AnalysisRunStepOutcome outcome;
        try
        {
            outcome = entry.StepId switch
            {
                AnalysisStepId.Capture => await this.ExecuteCaptureAsync(runId, run, userCancellationToken),
                AnalysisStepId.Discover => await this.ExecuteDiscoverAsync(runId, run, userCancellationToken),
                AnalysisStepId.Collect => await this.ExecuteCollectAsync(runId, run, userCancellationToken),
                _ => throw new InvalidOperationException($"The analysis pipeline has no implementation for step {entry.StepId}."),
            };
        }
        catch (OperationCanceledException) when (userCancellationToken.IsCancellationRequested)
        {
            outcome = new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Cancelled, null);
        }

        var finishResult = await store.FinishStepAsync(runId, outcome, this.Now(), CancellationToken.None);
        return finishResult.IsFailure
            ? new AnalysisRunStepOutcome(entry.StepId, AnalysisRunStepState.Failed, AnalysisFailureCode.UnexpectedFailure)
            : outcome;
    }

    private async Task<AnalysisRunStepOutcome> ExecuteCaptureAsync(Guid runId, AnalysisPipelineRun run, CancellationToken userCancellationToken)
    {
        var detailResult = await store.GetDetailAsync(runId, CancellationToken.None);
        if (detailResult.IsFailure)
        {
            throw new InvalidOperationException(
                "The analysis pipeline could not read the capture detail. Errors: " +
                string.Join(", ", detailResult.Errors.Select(error => error.Code)) + ".");
        }

        run.Detail = detailResult.Data!;
        var captureResult = await captureService.CaptureAsync(run.Detail, userCancellationToken);
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
            run.Capture = capture;
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
            run.Capture = capture;
            return this.CaptureOutcome(capture);
        }

        if (detail.Terminal is not null || detail.State != AnalysisRunState.Capturing)
        {
            return new AnalysisRunStepOutcome(AnalysisStepId.Capture, AnalysisRunStepState.Cancelled, null);
        }

        throw new InvalidOperationException("The analysis pipeline observed unexpected durable capture state drift.");
    }

    private async Task<AnalysisRunStepOutcome> ExecuteDiscoverAsync(Guid runId, AnalysisPipelineRun run, CancellationToken userCancellationToken)
    {
        var detail = run.Detail!;
        var capture = run.Capture!;
        var manifest = capture.Manifest;

        var anatomyResult = await anatomyService.AnalyzeAsync(detail.Repository, manifest, userCancellationToken);
        if (anatomyResult.IsFailure)
        {
            return Failed(AnalysisStepId.Discover, anatomyResult.Errors);
        }

        var anatomy = anatomyResult.Data!;
        var rankingResult = await rankingService.RankAsync(detail.Repository, manifest, anatomy, userCancellationToken);
        if (rankingResult.IsFailure)
        {
            return Failed(AnalysisStepId.Discover, rankingResult.Errors);
        }

        var ranking = rankingResult.Data!;
        EnsureRecorded(
            await store.RecordCorrespondenceCandidateCountAsync(runId, ranking.Candidates.Count, CancellationToken.None), "correspondence candidate");

        var graphResult = await graphService.BuildAsync(detail.Repository, manifest, anatomy, ranking, userCancellationToken);
        if (graphResult.IsFailure)
        {
            return Failed(AnalysisStepId.Discover, graphResult.Errors);
        }

        var graph = graphResult.Data!;
        var policy = policyService.Apply(graph, userCancellationToken);
        EnsureRecorded(
            await store.RecordDisclosedEvidenceNodeCountAsync(runId, policy.DisclosedNodes.Count, CancellationToken.None), "disclosed evidence node");

        var readerResult = treeReaderFactory.Open(detail.Repository, manifest);
        if (readerResult.IsFailure)
        {
            return Failed(AnalysisStepId.Discover, readerResult.Errors);
        }

        var afterTreeResult = await readerResult.Data!.ListTreeAsync(userCancellationToken);
        if (afterTreeResult.IsFailure)
        {
            return Failed(AnalysisStepId.Discover, afterTreeResult.Errors);
        }

        var binderRequest = new EvidenceBinderRequest(detail, capture, policy, afterTreeResult.Data!, anatomy, detail.ChangeContext);
        var binderResult = binderService.Assemble(binderRequest, userCancellationToken);
        if (binderResult.IsFailure)
        {
            return Failed(AnalysisStepId.Discover, binderResult.Errors);
        }

        run.Ranking = ranking;
        run.Graph = graph;
        run.Policy = policy;
        run.Binder = binderResult.Data!;
        logger.LogInformation(
            "Analysis run {RunId} discovered {CandidateCount} correspondence candidate(s) and disclosed {DisclosedCount} evidence node(s).",
            runId, ranking.Candidates.Count, policy.DisclosedNodes.Count);
        return new AnalysisRunStepOutcome(AnalysisStepId.Discover, AnalysisRunStepState.Succeeded, null);
    }

    private async Task<AnalysisRunStepOutcome> ExecuteCollectAsync(Guid runId, AnalysisPipelineRun run, CancellationToken userCancellationToken)
    {
        var binder = run.Binder!;
        var curateResult = await curatorService.CurateAsync(binder, userCancellationToken);
        if (curateResult.IsFailure)
        {
            return Failed(AnalysisStepId.Collect, curateResult.Errors);
        }

        var curation = curateResult.Data!;
        if (curation.Diagnostics.ParseFailureReason is not null)
        {
            return new AnalysisRunStepOutcome(AnalysisStepId.Collect, AnalysisRunStepState.Failed, AnalysisFailureCode.CuratorOutputUnreadable);
        }

        var validation = validationService.Validate(curation.Draft, binder, userCancellationToken);
        EnsureRecorded(
            await store.RecordValidationRemovalCountAsync(runId, validation.Removals.Count, CancellationToken.None), "validation removal");

        var publicationRequest = new PublicationRequest(validation, binder, run.Graph!, run.Policy!, run.Ranking!);
        var publicationResult = await publicationService.PublishAsync(publicationRequest, userCancellationToken);
        if (publicationResult.IsFailure)
        {
            return Failed(AnalysisStepId.Collect, publicationResult.Errors);
        }

        var readingModel = publicationResult.Data!.ReadingModel;
        var projectionResult = this.Project(readingModel, validation.Removals);
        if (projectionResult.IsFailure)
        {
            return Failed(AnalysisStepId.Collect, projectionResult.Errors);
        }

        var projection = projectionResult.Data!;
        if (!ReadingProjectionBudget.Fits(projection))
        {
            return new AnalysisRunStepOutcome(AnalysisStepId.Collect, AnalysisRunStepState.Failed, AnalysisFailureCode.ReadingModelTooLarge);
        }

        run.Projection = projection;
        logger.LogInformation("Analysis run {RunId} collected a reading model with {RemovalCount} removal(s) and {EvidenceCount} evidence node(s).",
            runId, validation.Removals.Count, readingModel.Evidence.Count);
        return new AnalysisRunStepOutcome(AnalysisStepId.Collect, AnalysisRunStepState.Succeeded, null);
    }

    private Result<AnalysisReadingProjection> Project(ReadingModel readingModel, IReadOnlyList<ValidationRemoval> removals)
    {
        var modelResult = ReadingModelProtocolMapper.ToProtocol(readingModel);
        if (modelResult.IsFailure)
        {
            return Result.ErrorFromResult<AnalysisReadingProjection>(modelResult);
        }

        var removalsResult = ReadingModelProtocolMapper.ToProtocol(removals);
        if (removalsResult.IsFailure)
        {
            return Result.ErrorFromResult<AnalysisReadingProjection>(removalsResult);
        }

        var modelJson = protocolSerializer.SerializeDocument(modelResult.Data!);
        if (modelJson.IsFailure)
        {
            return Result.ErrorFromResult<AnalysisReadingProjection>(modelJson);
        }

        var removalsJson = protocolSerializer.SerializeDocument(removalsResult.Data!);
        if (removalsJson.IsFailure)
        {
            return Result.ErrorFromResult<AnalysisReadingProjection>(removalsJson);
        }

        return new AnalysisReadingProjection(modelJson.Data!, removalsJson.Data!);
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
        await store.CommitTerminalAsync(runId, terminal, null, CancellationToken.None);
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
