using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.EngineStatus.Interfaces;

/// <summary>
///     Defines the engine readiness capability.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services and do not need to be thread-safe.
/// </remarks>
public interface IEngineStatusService
{
    /// <summary>
    ///     Asynchronously checks whether the engine is ready to perform work.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the readiness outcome.</returns>
    /// <exception cref="OperationCanceledException">
    ///     The <see cref="CancellationToken" /> is canceled.
    /// </exception>
    Task<Result> CheckStatusAsync(CancellationToken cancellationToken);
}
