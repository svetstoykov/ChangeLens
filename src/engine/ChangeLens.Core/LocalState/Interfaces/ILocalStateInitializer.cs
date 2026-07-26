using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.LocalState.Interfaces;

/// <summary>
///     Defines required local-state database initialization and readiness.
/// </summary>
public interface ILocalStateInitializer
{
    /// <summary>
    ///     Asynchronously initializes or validates the local-state database.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the readiness outcome.</returns>
    Task<Result> InitializeAsync(CancellationToken cancellationToken);
}
