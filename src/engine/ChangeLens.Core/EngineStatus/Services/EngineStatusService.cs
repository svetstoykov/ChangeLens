using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.EngineStatus.Services;

/// <summary>
///     Provides the engine readiness check.
/// </summary>
/// <remarks>
///     The Engine host registers this stateless implementation as a singleton. It is safe to call concurrently.
/// </remarks>
public sealed class EngineStatusService(ILocalStateInitializer localStateInitializer) : IEngineStatusService
{
    /// <inheritdoc />
    public Task<Result> CheckStatusAsync(CancellationToken cancellationToken) =>
        localStateInitializer.InitializeAsync(cancellationToken);
}
