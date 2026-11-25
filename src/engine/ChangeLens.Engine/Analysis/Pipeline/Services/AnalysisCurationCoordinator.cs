using ChangeLens.Core.Analysis.Curation.Interfaces;
using ChangeLens.Core.Analysis.Curation.Models;
using ChangeLens.Core.Analysis.DraftValidation.Interfaces;
using ChangeLens.Core.Analysis.EvidenceBinder.Interfaces;
using ChangeLens.Core.Analysis.FindingValidation.Interfaces;
using ChangeLens.Core.Analysis.Publication.Models;
using ChangeLens.Core.Analysis.Review.Interfaces;
using ChangeLens.Core.Analysis.Review.Models;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Analysis.Pipeline.Interfaces;
using ChangeLens.Engine.Analysis.Pipeline.Models;
using Microsoft.Extensions.Logging;
using EvidenceBinderModel = ChangeLens.Core.Analysis.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Engine.Analysis.Pipeline.Services;

/// <summary>
///     Runs the curator over the full binder concurrently with an optional changed-file-scoped reviewer, then validates
///     their drafts.
/// </summary>
/// <remarks>
///     <para>
///         When the curator fails or produces an unreadable draft, the coordinator cancels the linked reviewer token and
///         transfers disposal ownership of that <see cref="CancellationTokenSource" /> to a completion continuation on
///         the abandoned reviewer task so the token remains valid until the reviewer finishes.
///     </para>
///     <para>
///         Registered as a scoped service. The implementation may depend on services registered with any lifetime and
///         does not need to be thread-safe.
///     </para>
/// </remarks>
/// <param name="binderService">The evidence-binder service used to scope reviewer input. Cannot be <see langword="null" />.</param>
/// <param name="curatorService">The curator service. Cannot be <see langword="null" />.</param>
/// <param name="reviewerService">The reviewer service. Cannot be <see langword="null" />.</param>
/// <param name="validationService">The curator draft validation service. Cannot be <see langword="null" />.</param>
/// <param name="findingValidationService">The reviewer finding validation service. Cannot be <see langword="null" />.</param>
/// <param name="reviewerOptions">The immutable reviewer options. Cannot be <see langword="null" />.</param>
/// <param name="logger">The curation coordinator logger. Cannot be <see langword="null" />.</param>
internal sealed class AnalysisCurationCoordinator(
    IEvidenceBinderService binderService,
    ICuratorService curatorService,
    IReviewerService reviewerService,
    IDraftValidationService validationService,
    IFindingValidationService findingValidationService,
    ReviewerOptions reviewerOptions,
    ILogger<AnalysisCurationCoordinator> logger) : IAnalysisCurationCoordinator
{
    /// <inheritdoc />
    public async Task<AnalysisStepResult<CurationStepOutput>> ExecuteAsync(
        Guid runId, EvidenceBinderModel binder, CancellationToken userCancellationToken)
    {
        var reviewerBinder = reviewerOptions.Enabled ? binderService.ScopeToChangedFiles(binder) : binder;
        var reviewCancellation = CancellationTokenSource.CreateLinkedTokenSource(userCancellationToken);
        var reviewCancellationOwnershipTransferred = false;
        try
        {
            var curatorTask = Task.Run(() => curatorService.CurateAsync(binder, userCancellationToken), CancellationToken.None);
            var reviewerTask = reviewerOptions.Enabled
                ? Task.Run(() => reviewerService.ReviewAsync(reviewerBinder, reviewCancellation.Token), CancellationToken.None)
                : null;

            Result<CuratorOutcome> curateResult;
            try
            {
                curateResult = await curatorTask;
            }
            catch
            {
                reviewCancellationOwnershipTransferred = AbandonReview(reviewerTask, reviewCancellation);
                throw;
            }

            if (curateResult.IsFailure)
            {
                reviewCancellationOwnershipTransferred = AbandonReview(reviewerTask, reviewCancellation);
                return AnalysisStepResult<CurationStepOutput>.Failed(AnalysisStepId.Collect, curateResult.Errors);
            }

            var curation = curateResult.Data!;
            if (curation.Diagnostics.ParseFailureReason is not null)
            {
                reviewCancellationOwnershipTransferred = AbandonReview(reviewerTask, reviewCancellation);
                return AnalysisStepResult<CurationStepOutput>.Failed(
                    AnalysisStepId.Collect, AnalysisFailureCode.CuratorOutputUnreadable);
            }

            var review = await this.CompleteReviewAsync(runId, reviewerTask, reviewerBinder, userCancellationToken);
            var validation = validationService.Validate(curation.Draft, binder, userCancellationToken);
            return AnalysisStepResult<CurationStepOutput>.Completed(
                new AnalysisRunStepOutcome(AnalysisStepId.Collect, AnalysisRunStepState.Succeeded, null),
                new CurationStepOutput(validation, review));
        }
        finally
        {
            if (!reviewCancellationOwnershipTransferred)
            {
                reviewCancellation.Dispose();
            }
        }
    }

    private async Task<ReviewContribution> CompleteReviewAsync(
        Guid runId,
        Task<Result<ReviewerOutcome>>? reviewerTask,
        EvidenceBinderModel binder,
        CancellationToken userCancellationToken)
    {
        if (reviewerTask is null)
        {
            return ReviewContribution.NotRun;
        }

        var reviewResult = await reviewerTask;
        if (reviewResult.IsFailure)
        {
            logger.LogWarning(
                "Analysis run {RunId} continues without a review after reviewer errors {ErrorCodes}.",
                runId, reviewResult.Errors.Select(error => error.Code));
            return ReviewContribution.Failed;
        }

        var reviewer = reviewResult.Data!;
        if (reviewer.Diagnostics.ParseFailureReason is not null)
        {
            logger.LogWarning("Analysis run {RunId} continues without a review because the reviewer reply was unreadable.", runId);
            return ReviewContribution.Failed;
        }

        var findingValidation = findingValidationService.Validate(reviewer.Draft, binder, userCancellationToken);
        return new ReviewContribution(PublicationReview.Ran(findingValidation), findingValidation.Removals);
    }

    private static bool AbandonReview(Task<Result<ReviewerOutcome>>? reviewerTask, CancellationTokenSource reviewCancellation)
    {
        if (reviewerTask is null)
        {
            return false;
        }

        reviewCancellation.Cancel();
        _ = reviewerTask.ContinueWith(
            static (task, state) =>
            {
                _ = task.Exception;
                ((CancellationTokenSource)state!).Dispose();
            },
            reviewCancellation,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return true;
    }
}
