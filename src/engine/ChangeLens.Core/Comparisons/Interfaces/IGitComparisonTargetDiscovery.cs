using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Comparisons.Interfaces;

/// <summary>
///     Defines discovery of supported local Git comparison targets.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one request and do not need to be thread-safe.
/// </remarks>
public interface IGitComparisonTargetDiscovery
{
    /// <summary>
    ///     Asynchronously lists supported comparison targets for a selected repository.
    /// </summary>
    /// <param name="path">The selected repository directory path.</param>
    /// <param name="query">The exact optional target-name query.</param>
    /// <param name="after">The exact full-ref continuation cursor, or <see langword="null" />.</param>
    /// <param name="targetSetToken">The target-set token paired with <paramref name="after" />, or <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the ordered target set.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<ComparisonTargetSet>> ListAsync(
        string? path,
        string? query,
        string? after,
        string? targetSetToken,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously lists targets for an already inspected repository within a shared action budget.
    /// </summary>
    /// <param name="repository">The inspected repository descriptor. Cannot be <see langword="null" />.</param>
    /// <param name="query">The exact optional target-name query.</param>
    /// <param name="after">The exact full-ref continuation cursor, or <see langword="null" />.</param>
    /// <param name="targetSetToken">The target-set token paired with <paramref name="after" />, or <see langword="null" />.</param>
    /// <param name="startedAt">The monotonic timestamp at which the owning action began.</param>
    /// <param name="totalBudget">The positive total budget owned by the calling action.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the ordered target set.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="repository" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="totalBudget" /> is less than or equal to <see cref="TimeSpan.Zero" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<ComparisonTargetSet>> ListForRepositoryAsync(
        RepositoryDescriptor repository,
        string? query,
        string? after,
        string? targetSetToken,
        long startedAt,
        TimeSpan totalBudget,
        CancellationToken cancellationToken);
}
