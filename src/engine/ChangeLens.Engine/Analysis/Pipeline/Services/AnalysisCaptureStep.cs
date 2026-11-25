using ChangeLens.Core.Analysis.Snapshots.Constants;
using ChangeLens.Core.Analysis.Snapshots.Interfaces;
using ChangeLens.Core.Analysis.Snapshots.Models;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Engine.Analysis.Pipeline.Helpers;
using ChangeLens.Engine.Analysis.Pipeline.Interfaces;
using ChangeLens.Engine.Analysis.Pipeline.Models;

namespace ChangeLens.Engine.Analysis.Pipeline.Services;

/// <summary>
///     Captures and durably commits the frozen snapshot manifest for one analysis run.
/// </summary>
/// <remarks>
///     <para>
///         Resolves durable capture commit races by re-reading the stored run detail to distinguish durable cancellation,
///         an idempotent matching commit, or unexpected state drift.
///     </para>
///     <para>
///         Registered as a scoped service. The implementation may depend on services registered with any lifetime and
///         does not need to be thread-safe.
///     </para>
/// </remarks>
/// <param name="store">The durable analysis run store. Cannot be <see langword="null" />.</param>
/// <param name="captureService">The snapshot capture service. Cannot be <see langword="null" />.</param>
/// <param name="timeProvider">The time provider used for capture timestamps. Cannot be <see langword="null" />.</param>
internal sealed class AnalysisCaptureStep(
    IAnalysisRunStore store,
    ISnapshotCaptureService captureService,
    TimeProvider timeProvider) : IAnalysisCaptureStep
{
    /// <inheritdoc />
    public async Task<AnalysisStepResult<CaptureStepOutput>> ExecuteAsync(Guid runId, CancellationToken userCancellationToken)
    {
        var initialDetail = AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.GetDetailAsync(runId, CancellationToken.None), "read the capture detail");
        var captureRequest = new SnapshotCaptureRequest(
            initialDetail.RunId, initialDetail.Repository.CanonicalPath, initialDetail.Repository.CanonicalRepositoryPathKey,
            initialDetail.Comparison.Target, initialDetail.Comparison.TargetRevision, initialDetail.Repository.HeadRevision);
        var captureResult = await captureService.CaptureAsync(captureRequest, userCancellationToken);
        if (captureResult.IsFailure)
        {
            var failureCode = captureResult.Errors[0].Code switch
            {
                SnapshotErrorCode.StaleAtCapture => AnalysisFailureCode.StaleAtCapture,
                SnapshotErrorCode.CaptureFailed => AnalysisFailureCode.CaptureFailed,
                _ => captureResult.Errors[0].Code ?? AnalysisFailureCode.CaptureFailed,
            };
            return AnalysisStepResult<CaptureStepOutput>.Failed(AnalysisStepId.Capture, failureCode);
        }

        var capture = captureResult.Data!;
        var committed = AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.CommitCaptureAsync(runId, capture, this.Now(), CancellationToken.None), "commit the capture");
        if (committed)
        {
            return AnalysisStepResult<CaptureStepOutput>.Completed(
                CaptureOutcome(capture), new CaptureStepOutput(initialDetail, capture));
        }

        var detail = AnalysisPipelineStoreGuard.EnsureSuccess(
            await store.GetDetailAsync(runId, CancellationToken.None), "re-read the capture detail");
        if (detail.CancellationRequested && detail.Terminal is null)
        {
            return AnalysisStepResult<CaptureStepOutput>.Cancelled(AnalysisStepId.Capture);
        }

        if (detail.SnapshotId is not null && StringComparer.Ordinal.Equals(detail.ManifestHash, capture.Manifest.ManifestHash))
        {
            return AnalysisStepResult<CaptureStepOutput>.Completed(
                CaptureOutcome(capture), new CaptureStepOutput(initialDetail, capture));
        }

        if (detail.Terminal is not null || detail.State != AnalysisRunState.Capturing)
        {
            return AnalysisStepResult<CaptureStepOutput>.Cancelled(AnalysisStepId.Capture);
        }

        throw new InvalidOperationException("The analysis pipeline observed unexpected durable capture state drift.");
    }

    private long Now() => timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

    private static AnalysisRunStepOutcome CaptureOutcome(SnapshotCapture capture) =>
        capture.ExcludedUncommittedCounts.Total > 0
            ? new AnalysisRunStepOutcome(
                AnalysisStepId.Capture,
                AnalysisRunStepState.SucceededWithLimitations,
                AnalysisLimitationReason.UncommittedWorkExcluded)
            : new AnalysisRunStepOutcome(AnalysisStepId.Capture, AnalysisRunStepState.Succeeded, null);
}
