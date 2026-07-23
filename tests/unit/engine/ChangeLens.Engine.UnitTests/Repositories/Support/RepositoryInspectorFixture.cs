using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.UnitTests.Repositories.Support;

/// <summary>
///     Provides controllable repository-inspection collaborators for Engine action tests.
/// </summary>
internal sealed class RepositoryInspectorFixture : IGitCommandRunner, IRepositoryPathResolver
{
    private readonly Queue<Func<GitCommand, CancellationToken, Task<Result<GitCommandOutput>>>> _commandResults =
        new();
    private readonly Queue<Func<string, CancellationToken, Task<Result<string>>>> _pathResults = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="RepositoryInspectorFixture" /> class.
    /// </summary>
    internal RepositoryInspectorFixture()
    {
        Inspector = new GitRepositoryInspector(this, this);
    }

    /// <summary>
    ///     Gets the real Core inspector backed by the controllable collaborators.
    /// </summary>
    internal GitRepositoryInspector Inspector { get; }

    /// <summary>
    ///     Gets the Git commands received by the fixture in call order.
    /// </summary>
    internal List<GitCommand> Commands { get; } = new();

    /// <summary>
    ///     Gets the paths received by the fixture in call order.
    /// </summary>
    internal List<string> Paths { get; } = new();

    /// <summary>
    ///     Queues a fixed path-resolution result.
    /// </summary>
    /// <param name="result">The result returned by the next path-resolution call.</param>
    internal void EnqueuePath(Result<string> result) =>
        _pathResults.Enqueue((_, _) => Task.FromResult(result));

    /// <summary>
    ///     Queues a path-resolution callback.
    /// </summary>
    /// <param name="result">The callback invoked by the next path-resolution call.</param>
    internal void EnqueuePath(
        Func<string, CancellationToken, Task<Result<string>>> result) =>
        _pathResults.Enqueue(result);

    /// <summary>
    ///     Queues a fixed Git command result.
    /// </summary>
    /// <param name="result">The result returned by the next Git command.</param>
    internal void EnqueueCommand(Result<GitCommandOutput> result) =>
        _commandResults.Enqueue((_, _) => Task.FromResult(result));

    /// <summary>
    ///     Queues a Git command callback.
    /// </summary>
    /// <param name="result">The callback invoked by the next Git command.</param>
    internal void EnqueueCommand(
        Func<GitCommand, CancellationToken, Task<Result<GitCommandOutput>>> result) =>
        _commandResults.Enqueue(result);

    /// <summary>
    ///     Queues a complete successful repository inspection.
    /// </summary>
    /// <param name="revision">The full committed revision.</param>
    /// <param name="branchName">
    ///     The attached branch name, or <see langword="null" /> for a detached HEAD.
    /// </param>
    internal void EnqueueSuccessfulInspection(string revision, string? branchName)
    {
        EnqueuePath(Result.Success<string>("/physical/selection"));
        EnqueuePath(Result.Success<string>("/projects/change_lens"));
        EnqueueCommand(Output("git version 2.51.0\n"));
        EnqueueCommand(Output("true\n"));
        EnqueueCommand(Output("false\n"));
        EnqueueCommand(Output("/reported/change_lens\n"));
        EnqueueCommand(Output(revision + "\n"));
        EnqueueCommand(
            branchName is null
                ? Output(string.Empty, exitCode: 1)
                : Output(branchName + "\n"));
    }

    /// <inheritdoc />
    public Task<Result<GitCommandOutput>> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken)
    {
        Commands.Add(command);
        return _commandResults.Dequeue()(command, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<string>> ResolveAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Paths.Add(path);
        return _pathResults.Dequeue()(path, cancellationToken);
    }

    private static Result<GitCommandOutput> Output(string standardOutput, int exitCode = 0) =>
        Result.Success(new GitCommandOutput(exitCode, standardOutput, string.Empty));
}
