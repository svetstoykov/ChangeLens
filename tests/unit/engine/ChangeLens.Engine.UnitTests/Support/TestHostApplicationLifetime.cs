using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Provides a controlled host lifetime for protocol-service unit tests.
/// </summary>
internal sealed class TestHostApplicationLifetime : IHostApplicationLifetime
{
    private readonly TaskCompletionSource _stopRequested = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public CancellationToken ApplicationStarted => CancellationToken.None;

    /// <inheritdoc />
    public CancellationToken ApplicationStopping => CancellationToken.None;

    /// <inheritdoc />
    public CancellationToken ApplicationStopped => CancellationToken.None;

    /// <summary>
    ///     Gets a value indicating whether application stop was requested.
    /// </summary>
    internal bool StopRequested { get; private set; }

    /// <inheritdoc />
    public void StopApplication()
    {
        StopRequested = true;
        _stopRequested.TrySetResult();
    }

    /// <summary>
    ///     Waits until the host requests application shutdown.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting.</param>
    /// <returns>A task that represents the asynchronous wait.</returns>
    internal Task WaitForStopAsync(CancellationToken cancellationToken) =>
        _stopRequested.Task.WaitAsync(cancellationToken);
}
