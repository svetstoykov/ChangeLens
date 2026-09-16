using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Interfaces;

/// <summary>
///     Defines controlled execution of Git commands whose standard output may contain arbitrary bytes.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one request and do not need to be thread-safe.
/// </remarks>
public interface IGitBinaryCommandRunner
{
    /// <summary>
    ///     Asynchronously runs the given Git command and captures its bounded binary output.
    /// </summary>
    /// <param name="command">The immutable Git command to run. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the captured Git output on success.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<GitBinaryCommandOutput>> RunBinaryAsync(
        GitCommand command,
        CancellationToken cancellationToken);
}
