using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.Git.Services;

namespace ChangeLens.Infrastructure.IntegrationTests.Git.Support;

/// <summary>
///     Counts batch invocations while executing real Git commands.
/// </summary>
internal sealed class CountingGitBinaryCommandRunner : IGitBinaryCommandRunner
{
    private readonly GitCliCommandRunner _inner = new();

    /// <summary>
    ///     Gets the number of batch commands requested.
    /// </summary>
    internal int BatchCount { get; private set; }

    /// <inheritdoc />
    public Task<Result<GitBinaryCommandOutput>> RunBinaryAsync(GitCommand command, CancellationToken cancellationToken)
    {
        if (command.Arguments.Contains("--batch"))
        {
            this.BatchCount++;
        }

        return this._inner.RunBinaryAsync(command, cancellationToken);
    }
}
