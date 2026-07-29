using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Comparisons.Interfaces;

/// <summary>
///     Defines freshness checks for prepared local Git comparisons.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one request and do not need to be thread-safe.
/// </remarks>
public interface IGitComparisonFreshnessChecker
{
    /// <summary>
    ///     Asynchronously checks whether a prepared comparison freshness token remains current.
    /// </summary>
    /// <param name="path">The selected repository directory path.</param>
    /// <param name="target">The exact full local or cached remote-tracking reference.</param>
    /// <param name="freshnessToken">The lowercase SHA-256 token from comparison preparation.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains whether the prepared
    ///     comparison remains current.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<ComparisonFreshnessState>> CheckAsync(
        string? path,
        string? target,
        string? freshnessToken,
        CancellationToken cancellationToken);
}
