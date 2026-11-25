using System.Diagnostics;
using ChangeLens.Core.Analysis.Publication.Interfaces;
using ChangeLens.Core.Analysis.Publication.Models;
using ChangeLens.Core.Analysis.Shared.Models;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Analysis.Pipeline.Helpers;
using ChangeLens.Engine.Analysis.Pipeline.Interfaces;
using ChangeLens.Engine.Analysis.Pipeline.Models;
using ChangeLens.Engine.Analysis.ReadingModel.Helpers;
using ChangeLens.Engine.Protocol.Interfaces;
using Microsoft.Extensions.Logging;
using CoreReadingModel = ChangeLens.Core.Analysis.Publication.Models.ReadingModel;

namespace ChangeLens.Engine.Analysis.Pipeline.Services;

/// <summary>
///     Publishes the curated reading model, records validation removals, projects the protocol payload, and drops the
///     review if necessary to stay within the poll response budget.
/// </summary>
/// <remarks>
///     Registered as a scoped service. The implementation may depend on services registered with any lifetime and does
///     not need to be thread-safe.
/// </remarks>
/// <param name="store">The durable analysis run store. Cannot be <see langword="null" />.</param>
/// <param name="publicationService">The reading-model publication service. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The protocol serializer for reading-model documents. Cannot be <see langword="null" />.</param>
/// <param name="logger">The publication step logger. Cannot be <see langword="null" />.</param>
internal sealed class AnalysisPublicationStep(
    IAnalysisRunStore store,
    IPublicationService publicationService,
    IEngineProtocolSerializer protocolSerializer,
    ILogger<AnalysisPublicationStep> logger) : IAnalysisPublicationStep
{
    /// <inheritdoc />
    public async Task<AnalysisStepResult<AnalysisReadingProjection>> ExecuteAsync(
        Guid runId, DiscoverStepOutput discovery, CurationStepOutput curation, CancellationToken userCancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        ValidationRemoval[] removals = [.. curation.Validation.Removals, .. curation.Review.Removals];
        AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.RecordValidationRemovalCountAsync(runId, removals.Length, CancellationToken.None),
            "record the validation removal count");

        var publicationRequest = new PublicationRequest(
            curation.Validation, discovery.Binder, discovery.Graph, discovery.Policy, discovery.Ranking, curation.Review.Review);
        var publicationResult = await publicationService.PublishAsync(publicationRequest, userCancellationToken);
        if (publicationResult.IsFailure)
        {
            return AnalysisStepResult<AnalysisReadingProjection>.Failed(AnalysisStepId.Collect, publicationResult.Errors);
        }

        var publication = publicationResult.Data!;
        var readingModel = publication.ReadingModel;
        var projectionResult = this.Project(readingModel, removals);
        if (projectionResult.IsFailure)
        {
            return AnalysisStepResult<AnalysisReadingProjection>.Failed(AnalysisStepId.Collect, projectionResult.Errors);
        }

        var projection = projectionResult.Data!;
        var reviewUnavailable = curation.Review.Review.Status == ReadingReviewStatus.Failed;
        if (!ReadingProjectionBudget.Fits(projection))
        {
            if (curation.Review.Review.Status != ReadingReviewStatus.Ran)
            {
                return AnalysisStepResult<AnalysisReadingProjection>.Failed(
                    AnalysisStepId.Collect, AnalysisFailureCode.ReadingModelTooLarge);
            }

            readingModel = publicationService.Rebuild(publication.Explanation, discovery.Binder, discovery.Graph, PublicationReview.TooLarge);
            var fallbackResult = this.Project(readingModel, curation.Validation.Removals);
            if (fallbackResult.IsFailure)
            {
                return AnalysisStepResult<AnalysisReadingProjection>.Failed(AnalysisStepId.Collect, fallbackResult.Errors);
            }

            projection = fallbackResult.Data!;
            if (!ReadingProjectionBudget.Fits(projection))
            {
                return AnalysisStepResult<AnalysisReadingProjection>.Failed(
                    AnalysisStepId.Collect, AnalysisFailureCode.ReadingModelTooLarge);
            }

            logger.LogWarning(
                "Analysis run {RunId} dropped the review's {FindingCount} finding(s) and {RemovalCount} removal record(s) because the "
                + "reading projection exceeded the poll response budget.",
                runId, curation.Review.Review.Findings.Count, curation.Review.Removals.Count);
            reviewUnavailable = true;
        }

        logger.LogInformation(
            "Analysis run {RunId} collected a reading model with {RemovalCount} removal(s), {EvidenceCount} evidence node(s), "
            + "{FindingCount} finding(s), and review status {ReviewStatus} in {ElapsedMilliseconds:0.000} ms.",
            runId, removals.Length, readingModel.Evidence.Count, readingModel.Findings.Count, readingModel.Review.Status,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        var outcome = reviewUnavailable
            ? new AnalysisRunStepOutcome(
                AnalysisStepId.Collect,
                AnalysisRunStepState.SucceededWithLimitations,
                AnalysisLimitationReason.ReviewUnavailable)
            : new AnalysisRunStepOutcome(AnalysisStepId.Collect, AnalysisRunStepState.Succeeded, null);
        return AnalysisStepResult<AnalysisReadingProjection>.Completed(outcome, projection);
    }

    private Result<AnalysisReadingProjection> Project(CoreReadingModel readingModel, IReadOnlyList<ValidationRemoval> removals)
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
}
