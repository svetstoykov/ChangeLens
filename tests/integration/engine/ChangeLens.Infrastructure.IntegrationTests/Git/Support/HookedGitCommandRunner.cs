using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Infrastructure.IntegrationTests.Git.Support;

/// <summary>
///     Runs real Git commands and invokes a controlled test hook after each completed command.
/// </summary>
internal sealed class HookedGitCommandRunner(
    IGitCommandRunner inner,
    Func<GitCommand, int, CancellationToken, Task> afterCommand) : IGitCommandRunner
{
    private readonly IGitCommandRunner _inner =
        inner ?? throw new ArgumentNullException(nameof(inner));

    private readonly Func<GitCommand, int, CancellationToken, Task> _afterCommand =
        afterCommand ?? throw new ArgumentNullException(nameof(afterCommand));

    private int _completedCommandCount;

    /// <summary>
    ///     Gets the number of commands completed by the wrapped runner.
    /// </summary>
    internal int CompletedCommandCount => this._completedCommandCount;

    /// <inheritdoc />
    public async Task<Result<GitCommandOutput>> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken)
    {
        var result = await this._inner.RunAsync(command, cancellationToken);
        var completed = Interlocked.Increment(ref this._completedCommandCount);
        await this._afterCommand(command, completed, cancellationToken);
        return result;
    }
}
