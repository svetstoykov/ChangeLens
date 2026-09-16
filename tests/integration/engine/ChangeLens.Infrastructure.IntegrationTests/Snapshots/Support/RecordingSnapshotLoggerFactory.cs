using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;

/// <summary>
///     Creates one supplied logger for snapshot reader category requests.
/// </summary>
internal sealed class RecordingSnapshotLoggerFactory : ILoggerFactory
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a factory that returns the supplied logger.
    /// </summary>
    /// <param name="logger">The logger returned for every category. Cannot be <see langword="null" />.</param>
    internal RecordingSnapshotLoggerFactory(ILogger logger)
    {
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => this._logger;

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
