namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Provides protocol output that can fail independently while writing or flushing.
/// </summary>
internal sealed class ThrowingTextWriter(
    Exception? writeException = null,
    Exception? flushException = null) : StringWriter
{
    /// <summary>
    ///     Gets the number of flush attempts.
    /// </summary>
    internal int FlushCount { get; private set; }

    /// <inheritdoc />
    public override Task WriteLineAsync(
        ReadOnlyMemory<char> buffer,
        CancellationToken cancellationToken = default) =>
        writeException is null
            ? base.WriteLineAsync(buffer, cancellationToken)
            : Task.FromException(writeException);

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        FlushCount++;
        return flushException is null
            ? base.FlushAsync(cancellationToken)
            : Task.FromException(flushException);
    }
}
