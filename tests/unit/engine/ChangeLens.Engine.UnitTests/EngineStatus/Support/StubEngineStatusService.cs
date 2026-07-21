using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.UnitTests.EngineStatus.Support;

/// <summary>
///     Provides a controlled engine-status result for protocol tests.
/// </summary>
internal sealed class StubEngineStatusService(
    Func<CancellationToken, Task<Result>> checkStatusAsync) : IEngineStatusService
{
    /// <inheritdoc />
    public Task<Result> CheckStatusAsync(CancellationToken cancellationToken) =>
        checkStatusAsync(cancellationToken);
}
