using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Captures error-level log calls for a service under test.
/// </summary>
/// <typeparam name="T">The logger category type.</typeparam>
internal sealed class TestLogger<T> : ILogger<T>
{
    /// <summary>
    ///     Gets the formatted captured log messages in call order.
    /// </summary>
    internal List<string> Entries { get; } = new();

    /// <summary>
    ///     Gets the number of captured error or critical log calls.
    /// </summary>
    internal int ErrorCount { get; private set; }

    /// <summary>
    ///     Gets the last captured exception.
    /// </summary>
    internal Exception? LastException { get; private set; }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

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
        this.Entries.Add(formatter(state, exception));

        if (logLevel < LogLevel.Error)
        {
            return;
        }

        this.ErrorCount++;
        this.LastException = exception;
    }
}
