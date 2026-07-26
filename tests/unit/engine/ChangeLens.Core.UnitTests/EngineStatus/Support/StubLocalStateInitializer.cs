using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.UnitTests.EngineStatus.Support;

internal sealed class StubLocalStateInitializer(
    Func<CancellationToken, Task<Result>> initializeAsync) : ILocalStateInitializer
{
    public Task<Result> InitializeAsync(CancellationToken cancellationToken) =>
        initializeAsync(cancellationToken);
}
