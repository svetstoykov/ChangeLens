using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Interfaces;

/// <summary>
///     Defines controlled execution of the installed Git executable.
/// </summary>
/// <remarks>
///     <para>
///         Implementations are registered as singletons and must be safe for concurrent calls.
///     </para>
/// </remarks>
public interface IGitCommandRunner
{
    /// <summary>
    ///     Asynchronously runs the given Git command and captures its bounded output.
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
    Task<Result<GitCommandOutput>> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken);
}
