using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.UnitTests.Git.Support;

/// <summary>
///     Provides queued canonical path results for repository inspection tests.
/// </summary>
internal sealed class StubRepositoryPathResolver : IRepositoryPathResolver
{
    private readonly Queue<Func<string, CancellationToken, Task<Result<string>>>> _results = new();

    /// <summary>
    ///     Gets the paths received by the stub in call order.
    /// </summary>
    internal List<string> Paths { get; } = new();

    /// <summary>
    ///     Adds a fixed path result to the queue.
    /// </summary>
    /// <param name="result">The result returned by the next call.</param>
    internal void Enqueue(Result<string> result) =>
        _results.Enqueue((_, _) => Task.FromResult(result));

    /// <summary>
    ///     Adds a path callback to the queue.
    /// </summary>
    /// <param name="result">The callback invoked by the next call. Cannot be <see langword="null" />.</param>
    internal void Enqueue(
        Func<string, CancellationToken, Task<Result<string>>> result) =>
        _results.Enqueue(result);

    /// <inheritdoc />
    public Task<Result<string>> ResolveAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Paths.Add(path);
        return _results.Dequeue()(path, cancellationToken);
    }
}
