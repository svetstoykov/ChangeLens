using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Comparisons.Interfaces;

/// <summary>
///     Defines preparation of immutable local Git comparison facts.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one request and do not need to be thread-safe.
/// </remarks>
public interface IGitComparisonPreparer
{
    /// <summary>
    ///     Asynchronously prepares an exact merge-base comparison for a selected Git reference.
    /// </summary>
    /// <param name="path">The selected repository directory path.</param>
    /// <param name="target">The exact full local or cached remote-tracking reference.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains stable comparison facts.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<PreparedComparison>> PrepareAsync(string? path, string? target, CancellationToken cancellationToken);
}
