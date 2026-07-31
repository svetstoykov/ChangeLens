using System.Threading.Channels;
using ChangeLens.Engine.AnalysisRuns.Interfaces;

namespace ChangeLens.Engine.AnalysisRuns.Services;

/// <summary>
///     Provides the wake-up signal and single-run cancellation slot shared by request handling and the processor.
/// </summary>
internal sealed class AnalysisProcessorControl : IAnalysisProcessorControl
{
    private readonly Channel<byte> _channel = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Lock _slot = new();
    private Guid? _runningRunId;
    private CancellationTokenSource? _runningRunSource;

    /// <inheritdoc />
    public void SignalPendingWork() => this._channel.Writer.TryWrite(0);

    /// <inheritdoc />
    public async Task WaitForPendingWorkAsync(CancellationToken cancellationToken)
    {
        await this._channel.Reader.WaitToReadAsync(cancellationToken);
        this._channel.Reader.TryRead(out _);
    }

    /// <inheritdoc />
    public CancellationToken BeginRun(Guid runId)
    {
        var source = new CancellationTokenSource();
        lock (this._slot)
        {
            this._runningRunId = runId;
            this._runningRunSource = source;
        }

        return source.Token;
    }

    /// <inheritdoc />
    public void EndRun(Guid runId)
    {
        lock (this._slot)
        {
            if (this._runningRunId == runId)
            {
                this._runningRunId = null;
                this._runningRunSource = null;
            }
        }
    }

    /// <inheritdoc />
    public void RequestRunCancellation(Guid runId)
    {
        CancellationTokenSource? source;
        lock (this._slot)
        {
            source = this._runningRunId == runId ? this._runningRunSource : null;
        }

        source?.Cancel();
    }
}
