namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Provides protocol input that throws a configured read failure.
/// </summary>
internal sealed class ThrowingTextReader(IOException exception) : TextReader
{
    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(
        Memory<char> buffer,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<int>(exception);
}
