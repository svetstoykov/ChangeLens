using ChangeLens.Core.EngineStatus.Services;
using Xunit;

namespace ChangeLens.Core.UnitTests.EngineStatus.Services;

/// <summary>
///     Verifies the engine readiness capability.
/// </summary>
public sealed class EngineStatusServiceTests
{
    /// <summary>
    ///     Verifies that the engine reports readiness when its checks succeed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task CheckStatusAsyncReturnsSuccess()
    {
        var service = new EngineStatusService();

        var result = await service.CheckStatusAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    ///     Verifies that a canceled readiness check preserves cancellation.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task CheckStatusAsyncObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var service = new EngineStatusService();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.CheckStatusAsync(cancellation.Token));
    }
}
