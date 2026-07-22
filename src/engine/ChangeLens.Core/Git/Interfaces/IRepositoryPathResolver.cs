using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Interfaces;

/// <summary>
///     Defines physical canonicalization of accessible repository directory paths.
/// </summary>
/// <remarks>
///     <para>
///         Implementations are registered as singletons and must be safe for concurrent calls.
///     </para>
/// </remarks>
public interface IRepositoryPathResolver
{
    /// <summary>
    ///     Asynchronously resolves an existing directory to its canonical physical path.
    /// </summary>
    /// <param name="path">The directory path to resolve. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the canonical directory path on success.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<string>> ResolveAsync(
        string path,
        CancellationToken cancellationToken);
}
