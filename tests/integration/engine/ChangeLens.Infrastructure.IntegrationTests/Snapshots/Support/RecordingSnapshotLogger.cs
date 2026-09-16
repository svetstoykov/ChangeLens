using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;

/// <summary>
///     Records formatted entries emitted by one snapshot reader logger.
/// </summary>
/// <typeparam name="T">The category type associated with the logger.</typeparam>
internal sealed class RecordingSnapshotLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    /// <summary>
    ///     Gets the recorded entries in emission order.
    /// </summary>
    internal IReadOnlyList<(LogLevel Level, string Message)> Entries => this._entries;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        this._entries.Add((logLevel, formatter(state, exception)));
    }
}
