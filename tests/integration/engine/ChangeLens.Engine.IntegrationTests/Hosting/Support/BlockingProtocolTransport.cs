using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.IntegrationTests.Hosting.Support;

/// <summary>
///     Keeps the protocol host alive while hosting tests exercise the analysis processor.
/// </summary>
/// <param name="beforeReadAsync">
///     The asynchronous condition that must complete before protocol input is observed, or <see langword="null" />
///     when no condition is required.
/// </param>
internal sealed class BlockingProtocolTransport(Func<CancellationToken, Task>? beforeReadAsync = null) : IEngineProtocolTransport
{
    private readonly TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    ///     Asynchronously waits until the protocol host begins observing input.
    /// </summary>
    /// <param name="cancellationToken">The token that bounds the wait.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task WaitForReadAsync(CancellationToken cancellationToken) => this._readStarted.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Result<EngineProtocolRequest?>> ReadAsync(CancellationToken cancellationToken)
    {
        if (beforeReadAsync is not null)
        {
            await beforeReadAsync(cancellationToken);
        }

        this._readStarted.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return Result.Success<EngineProtocolRequest?>(null);
    }

    /// <inheritdoc />
    public Task<Result> WriteAsync(ProtocolResponse response, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
