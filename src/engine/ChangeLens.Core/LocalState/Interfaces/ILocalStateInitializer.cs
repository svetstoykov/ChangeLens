using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.LocalState.Interfaces;

/// <summary>
///     Defines required boot-time local-state database initialization.
/// </summary>
/// <remarks>
///     Implementations are resolved from a dedicated boot scope and called once before request processing. They do not
///     need to be thread-safe.
/// </remarks>
public interface ILocalStateInitializer
{
    /// <summary>
    ///     Asynchronously creates and migrates the local-state database if needed.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the initialization outcome.</returns>
    /// <remarks>
    ///     Call this once before any request-scoped local-state access.
    /// </remarks>
    Task<Result> InitializeAsync(CancellationToken cancellationToken);
}
