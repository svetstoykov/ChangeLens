using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Records the levels emitted through one test logger.
/// </summary>
/// <typeparam name="T">The category type associated with the logger.</typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LogLevel> _levels = [];
    private readonly List<Exception?> _exceptions = [];

    /// <summary>
    ///     Gets the emitted log levels in call order.
    /// </summary>
    internal IReadOnlyList<LogLevel> Levels => this._levels;

    /// <summary>
    ///     Gets the exceptions emitted in call order.
    /// </summary>
    internal IReadOnlyList<Exception?> Exceptions => this._exceptions;

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
        this._levels.Add(logLevel);
        this._exceptions.Add(exception);
    }
}
