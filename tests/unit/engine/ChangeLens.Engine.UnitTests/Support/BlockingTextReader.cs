namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Provides protocol input that waits until its read is canceled.
/// </summary>
internal sealed class BlockingTextReader : TextReader
{
    /// <inheritdoc />
    public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return null;
    }
}
