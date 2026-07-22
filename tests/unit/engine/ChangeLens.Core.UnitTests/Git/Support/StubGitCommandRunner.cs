using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.UnitTests.Git.Support;

/// <summary>
///     Provides queued Git command results for repository inspection tests.
/// </summary>
internal sealed class StubGitCommandRunner : IGitCommandRunner
{
    private readonly Queue<Func<GitCommand, CancellationToken, Task<Result<GitCommandOutput>>>> _results = new();

    /// <summary>
    ///     Gets the commands received by the stub in call order.
    /// </summary>
    internal List<GitCommand> Commands { get; } = new();

    /// <summary>
    ///     Adds a fixed command result to the queue.
    /// </summary>
    /// <param name="result">The result returned by the next call.</param>
    internal void Enqueue(Result<GitCommandOutput> result) =>
        _results.Enqueue((_, _) => Task.FromResult(result));

    /// <summary>
    ///     Adds a command callback to the queue.
    /// </summary>
    /// <param name="result">The callback invoked by the next call. Cannot be <see langword="null" />.</param>
    internal void Enqueue(
        Func<GitCommand, CancellationToken, Task<Result<GitCommandOutput>>> result) =>
        _results.Enqueue(result);

    /// <inheritdoc />
    public Task<Result<GitCommandOutput>> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken)
    {
        Commands.Add(command);
        return _results.Dequeue()(command, cancellationToken);
    }
}
