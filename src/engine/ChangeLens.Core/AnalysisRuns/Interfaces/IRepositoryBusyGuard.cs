using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.AnalysisRuns.Interfaces;

/// <summary>
///     Defines the repository-busy check every repository-scoped action performs before capability work.
/// </summary>
public interface IRepositoryBusyGuard
{
    /// <summary>Asynchronously checks reservation for a repository identified by its selected directory path.</summary>
    /// <param name="path">The selected repository directory path, or <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>
    ///     A task whose result is a success when the path does not resolve or is not reserved; otherwise, a failure
    ///     with <c>repository.busy</c>.
    /// </returns>
    Task<Result> CheckPathAsync(string? path, CancellationToken cancellationToken);

    /// <summary>Asynchronously checks reservation for a repository identified by its retained repository ID.</summary>
    /// <param name="repositoryId">The retained repository identifier, or <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>
    ///     A task whose result is a success when the identifier does not resolve or is not reserved; otherwise, a
    ///     failure with <c>repository.busy</c>.
    /// </returns>
    Task<Result> CheckRepositoryIdAsync(string? repositoryId, CancellationToken cancellationToken);
}
