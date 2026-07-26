using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Interfaces;

/// <summary>
///     Defines repository inspection operations for approved local Git facts.
/// </summary>
public interface IGitRepositoryInspector
{
    /// <summary>
    ///     Asynchronously inspects the repository selected by the given directory path.
    /// </summary>
    /// <param name="path">
    ///     The selected repository directory path, or <see langword="null" /> when none was supplied. Invalid values return
    ///     a validation error with code <c>repository.invalidPath</c>.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the canonical repository identity
    ///     and committed HEAD state on success.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<RepositoryDescriptor>> InspectAsync(string? path, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously inspects the repository selected by the given directory path within a supplied budget.
    /// </summary>
    /// <param name="path">
    ///     The selected repository directory path, or <see langword="null" /> when none was supplied.
    /// </param>
    /// <param name="totalBudget">The positive total time allowed for path resolution and Git facts.</param>
    /// <param name="errorPolicy">
    ///     The terminal Git errors selected by the calling capability. Cannot be <see langword="null" />.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the canonical repository identity
    ///     and committed HEAD state on success.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="totalBudget" /> is less than or equal to <see cref="TimeSpan.Zero" />.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="errorPolicy" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<RepositoryDescriptor>> InspectAsync(
        string? path,
        TimeSpan totalBudget,
        GitCommandErrorPolicy errorPolicy,
        CancellationToken cancellationToken);
}
