using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.LocalState.Interfaces;

/// <summary>
///     Defines required local-state database initialization and readiness.
/// </summary>
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
    ///     Call this once, before any other local-state access. Use <see cref="CheckReadinessAsync" /> to verify the
    ///     database remains reachable afterward.
    /// </remarks>
    Task<Result> InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously verifies that the local-state database remains reachable.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the readiness outcome.</returns>
    /// <remarks>
    ///     This is a cheap reachability check. It reports the stable unavailable error when
    ///     <see cref="InitializeAsync" /> has not yet completed successfully.
    /// </remarks>
    Task<Result> CheckReadinessAsync(CancellationToken cancellationToken);
}
