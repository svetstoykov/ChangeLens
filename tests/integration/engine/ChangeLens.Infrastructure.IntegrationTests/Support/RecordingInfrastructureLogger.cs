using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.IntegrationTests.Support;

/// <summary>
///     Captures structured log entries and exceptions emitted during infrastructure integration tests.
/// </summary>
/// <typeparam name="T">The logger category type.</typeparam>
internal sealed class RecordingInfrastructureLogger<T> : ILogger<T>
{
    private readonly List<RecordedInfrastructureLogEntry> _entries = [];
    private readonly Lock _sync = new();

    /// <summary>
    ///     Gets a snapshot of recorded entries in emission order.
    /// </summary>
    internal IReadOnlyList<RecordedInfrastructureLogEntry> Entries
    {
        get
        {
            lock (this._sync)
            {
                return [.. this._entries];
            }
        }
    }

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
        lock (this._sync)
        {
            this._entries.Add(new RecordedInfrastructureLogEntry(logLevel, formatter(state, exception), exception));
        }
    }
}

/// <summary>
///     Represents one captured log entry from an infrastructure adapter test.
/// </summary>
/// <param name="Level">The emitted log level.</param>
/// <param name="Message">The formatted log message.</param>
/// <param name="Exception">The captured exception, if any.</param>
internal sealed record RecordedInfrastructureLogEntry(LogLevel Level, string Message, Exception? Exception);
