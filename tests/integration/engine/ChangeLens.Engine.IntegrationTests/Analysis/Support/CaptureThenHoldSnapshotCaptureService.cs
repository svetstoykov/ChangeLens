using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Wraps the real snapshot capture service, publishing a successful capture and holding it until the test
///     releases it.
/// </summary>
internal sealed class CaptureThenHoldSnapshotCaptureService : ISnapshotCaptureService
{
    private readonly ISnapshotCaptureService _inner;
    private readonly TaskCompletionSource _captured = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    ///     Initializes a new instance of the <see cref="CaptureThenHoldSnapshotCaptureService" /> class.
    /// </summary>
    /// <param name="inner">The real capture service the hold delegates to. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner" /> is <see langword="null" />.</exception>
    internal CaptureThenHoldSnapshotCaptureService(ISnapshotCaptureService inner) =>
        this._inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>
    ///     Gets a task that completes once a successful capture has been published.
    /// </summary>
    internal Task Captured => this._captured.Task;

    /// <summary>
    ///     Gets the capture published after the inner capture succeeded, or <see langword="null" /> before then.
    /// </summary>
    internal SnapshotCapture? Capture { get; private set; }

    /// <summary>
    ///     Releases a held capture.
    /// </summary>
    internal void Release() => this._release.TrySetResult();

    /// <inheritdoc />
    public async Task<Result<SnapshotCapture>> CaptureAsync(AnalysisRunDetail run, CancellationToken cancellationToken)
    {
        var result = await this._inner.CaptureAsync(run, cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        this.Capture = result.Data!;
        this._captured.TrySetResult();
        await this._release.Task.WaitAsync(cancellationToken);
        return result;
    }
}