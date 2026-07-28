using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Provides an engine-status capability that always reports readiness.
/// </summary>
/// <remarks>
///     The routing map is built before the processor's constructor body runs, so this collaborator is never called by
///     the map-validation tests. It exists so those tests can construct the processor without a null dependency.
/// </remarks>
internal sealed class StubEngineStatusService : IEngineStatusService
{
    /// <inheritdoc />
    public Task<Result> CheckStatusAsync(CancellationToken cancellationToken) => Task.FromResult(Result.Success());
}
