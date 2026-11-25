using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Repositories.Interfaces;

/// <summary>
///     Defines repository inspection operations that resolve a directory path into a canonical repository descriptor.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one request and do not need to be thread-safe.
/// </remarks>
public interface IRepositoryInspector
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
}
