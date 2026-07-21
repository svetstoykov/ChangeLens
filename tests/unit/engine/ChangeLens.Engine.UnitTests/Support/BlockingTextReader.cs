namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Provides protocol input that waits until its read is canceled.
/// </summary>
internal sealed class BlockingTextReader : TextReader
{
    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(
        Memory<char> buffer,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return 0;
    }
}
