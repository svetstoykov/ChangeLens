using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Provides a controlled host lifetime for protocol-service unit tests.
/// </summary>
internal sealed class TestHostApplicationLifetime : IHostApplicationLifetime
{
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
    public void StopApplication() => StopRequested = true;
}
