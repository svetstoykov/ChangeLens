using System.Diagnostics;
using ChangeLens.Core.Analysis.ChangeAnatomy.Interfaces;
using ChangeLens.Core.Analysis.ContextPolicy.Interfaces;
using ChangeLens.Core.Analysis.Correspondence.Interfaces;
using ChangeLens.Core.Analysis.EvidenceBinder.Interfaces;
using ChangeLens.Core.Analysis.EvidenceBinder.Models;
using ChangeLens.Core.Analysis.EvidenceGraph.Interfaces;
using ChangeLens.Core.Analysis.Snapshots.Interfaces;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Engine.Analysis.Pipeline.Helpers;
using ChangeLens.Engine.Analysis.Pipeline.Interfaces;
using ChangeLens.Engine.Analysis.Pipeline.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Analysis.Pipeline.Services;

/// <summary>
///     Executes the deterministic discovery stages to analyze change anatomy, rank correspondence, build and disclose the
///     evidence graph, list the frozen tree, and assemble the evidence binder.
/// </summary>
/// <remarks>
///     Registered as a scoped service. The implementation may depend on services registered with any lifetime and does
///     not need to be thread-safe.
/// </remarks>
/// <param name="store">The durable analysis run store. Cannot be <see langword="null" />.</param>
/// <param name="anatomyService">The change-anatomy analysis service. Cannot be <see langword="null" />.</param>
/// <param name="rankingService">The correspondence-ranking service. Cannot be <see langword="null" />.</param>
/// <param name="graphService">The evidence-graph builder service. Cannot be <see langword="null" />.</param>
/// <param name="policyService">The context-disclosure policy service. Cannot be <see langword="null" />.</param>
/// <param name="treeReaderFactory">The factory for frozen-revision tree readers. Cannot be <see langword="null" />.</param>
/// <param name="binderService">The evidence-binder assembly service. Cannot be <see langword="null" />.</param>
/// <param name="logger">The discovery step logger. Cannot be <see langword="null" />.</param>
internal sealed class AnalysisDiscoverStep(
    IAnalysisRunStore store,
    IChangeAnatomyService anatomyService,
    ICorrespondenceRankingService rankingService,
    IEvidenceGraphService graphService,
    IContextPolicyService policyService,
    IFrozenGitTreeReaderFactory treeReaderFactory,
    IEvidenceBinderService binderService,
    ILogger<AnalysisDiscoverStep> logger) : IAnalysisDiscoverStep
{
    /// <inheritdoc />
    public async Task<AnalysisStepResult<DiscoverStepOutput>> ExecuteAsync(
        Guid runId, CaptureStepOutput captureOutput, CancellationToken userCancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var detail = captureOutput.Detail;
        var capture = captureOutput.Capture;
        var manifest = capture.Manifest;
        var canonicalPath = detail.Repository.CanonicalPath;

        var anatomyResult = await anatomyService.AnalyzeAsync(canonicalPath, manifest, userCancellationToken);
        if (anatomyResult.IsFailure)
        {
            return AnalysisStepResult<DiscoverStepOutput>.Failed(AnalysisStepId.Discover, anatomyResult.Errors);
        }

        var anatomy = anatomyResult.Data!;
        var rankingResult = await rankingService.RankAsync(canonicalPath, manifest, anatomy, userCancellationToken);
        if (rankingResult.IsFailure)
        {
            return AnalysisStepResult<DiscoverStepOutput>.Failed(AnalysisStepId.Discover, rankingResult.Errors);
        }

        var ranking = rankingResult.Data!;
        AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.RecordCorrespondenceCandidateCountAsync(runId, ranking.Candidates.Count, CancellationToken.None),
            "record the correspondence candidate count");

        var graphResult = await graphService.BuildAsync(canonicalPath, manifest, anatomy, ranking, userCancellationToken);
        if (graphResult.IsFailure)
        {
            return AnalysisStepResult<DiscoverStepOutput>.Failed(AnalysisStepId.Discover, graphResult.Errors);
        }

        var graph = graphResult.Data!;
        var policy = policyService.Apply(graph, userCancellationToken);
        AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.RecordDisclosedEvidenceNodeCountAsync(runId, policy.DisclosedNodes.Count, CancellationToken.None),
            "record the disclosed evidence node count");

        var readerResult = treeReaderFactory.Open(canonicalPath, manifest);
        if (readerResult.IsFailure)
        {
            return AnalysisStepResult<DiscoverStepOutput>.Failed(AnalysisStepId.Discover, readerResult.Errors);
        }

        var afterTreeResult = await readerResult.Data!.ListTreeAsync(userCancellationToken);
        if (afterTreeResult.IsFailure)
        {
            return AnalysisStepResult<DiscoverStepOutput>.Failed(AnalysisStepId.Discover, afterTreeResult.Errors);
        }

        var runContext = new EvidenceBinderRunContext(detail.RunId, detail.Comparison.Target, detail.CapturedAtUnixMilliseconds ?? 0);
        var binderRequest = new EvidenceBinderRequest(
            runContext, capture, policy, afterTreeResult.Data!, anatomy, detail.ChangeContext);
        var binderResult = binderService.Assemble(binderRequest, userCancellationToken);
        if (binderResult.IsFailure)
        {
            return AnalysisStepResult<DiscoverStepOutput>.Failed(AnalysisStepId.Discover, binderResult.Errors);
        }

        logger.LogInformation(
            "Analysis run {RunId} discovered {CandidateCount} correspondence candidate(s) and disclosed {DisclosedCount} evidence node(s) in "
            + "{ElapsedMilliseconds:0.000} ms.",
            runId, ranking.Candidates.Count, policy.DisclosedNodes.Count, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        return AnalysisStepResult<DiscoverStepOutput>.Completed(
            new AnalysisRunStepOutcome(AnalysisStepId.Discover, AnalysisRunStepState.Succeeded, null),
            new DiscoverStepOutput(ranking, graph, policy, binderResult.Data!));
    }
}
