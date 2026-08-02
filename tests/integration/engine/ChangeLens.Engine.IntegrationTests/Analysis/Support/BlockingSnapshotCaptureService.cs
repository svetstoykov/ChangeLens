using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Provides a capture service whose result and timing one test controls precisely.
/// </summary>
internal sealed class BlockingSnapshotCaptureService : ISnapshotCaptureService
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets or sets the result capture returns once released.</summary>
    internal Result<SnapshotCapture> Outcome { get; set; } = Result.Fail<SnapshotCapture>(
        OperationError.InternalError("The controlled capture outcome was not configured.", "test.captureNotConfigured"));

    /// <summary>Gets or sets the outcome factory invoked with the durable capture detail.</summary>
    internal Func<AnalysisRunDetail, Result<SnapshotCapture>>? OutcomeFactory { get; set; }

    /// <summary>Gets a task that completes once capture has been entered.</summary>
    internal Task Entered => this._entered.Task;

    /// <summary>Releases a blocked capture.</summary>
    internal void Release() => this._release.TrySetResult();

    /// <inheritdoc />
    public async Task<Result<SnapshotCapture>> CaptureAsync(AnalysisRunDetail run, CancellationToken cancellationToken)
    {
        this._entered.TrySetResult();
        await this._release.Task.WaitAsync(cancellationToken);
        return this.OutcomeFactory?.Invoke(run) ?? this.Outcome;
    }
}
