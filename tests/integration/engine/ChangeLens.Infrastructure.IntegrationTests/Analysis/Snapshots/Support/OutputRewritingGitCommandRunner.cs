using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Infrastructure.IntegrationTests.Analysis.Snapshots.Support;

/// <summary>
///     Runs real Git commands and replaces the standard output of each successful command the predicate selects.
/// </summary>
/// <param name="inner">The runner that executes the real command.</param>
/// <param name="selects">Selects the commands whose output is replaced.</param>
/// <param name="replacementOutput">The standard output returned for selected commands.</param>
internal sealed class OutputRewritingGitCommandRunner(
    IGitCommandRunner inner,
    Func<GitCommand, bool> selects,
    string replacementOutput) : IGitCommandRunner
{
    /// <inheritdoc />
    public async Task<Result<GitCommandOutput>> RunAsync(GitCommand command, CancellationToken cancellationToken)
    {
        var result = await inner.RunAsync(command, cancellationToken);
        return result.IsSuccess && selects(command)
            ? Result.Success(result.Data! with { StandardOutput = replacementOutput })
            : result;
    }
}
