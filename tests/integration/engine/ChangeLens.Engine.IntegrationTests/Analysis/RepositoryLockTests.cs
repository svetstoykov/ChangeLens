using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.AnalysisRuns.Services;
using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.IntegrationTests.Analysis.Support;
using ChangeLens.Engine.IntegrationTests.Hosting.Support;
using ChangeLens.Engine.Protocol.Models;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis;

/// <summary>
///     Verifies the observable repository-reservation matrix through the production protocol host.
/// </summary>
public sealed class RepositoryLockTests
{
    /// <summary>
    ///     Asynchronously verifies every classified action rejects work against a reserved repository.
    /// </summary>
    /// <param name="action">The classified action to route.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Theory]
    [InlineData("repositories.open")]
    [InlineData("repositories.removeRecent")]
    [InlineData("comparisons.listTargets")]
    [InlineData("comparisons.prepare")]
    [InlineData("comparisons.checkFreshness")]
    [InlineData("comparisons.checkRemoteBaseline")]
    [InlineData("comparisons.refreshRemoteBaseline")]
    public async Task LockedRepositoryRejectsEveryClassifiedAction(string action)
    {
        await using var fixture = await EngineHostTestFixture.CreateBlockingPipelineAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        await fixture.AcceptRunAsync(new AnalysisCheckSelection(false, false));

        var response = await fixture.SendRealProtocolRequestAsync(action, fixture.RequestParametersFor(action));

        fixture.AssertBusyError(response);
        fixture.ReleaseBlockingPipeline();
        await fixture.Host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Asynchronously verifies observation and unrelated actions remain available while a repository is reserved.
    /// </summary>
    /// <param name="action">The allowed action to route.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Theory]
    [InlineData("repositories.listRecent")]
    [InlineData("preferences.getColorTheme")]
    [InlineData("preferences.setColorTheme")]
    [InlineData("engine.checkStatus")]
    [InlineData("analysis.getActive")]
    [InlineData("analysis.pollRun")]
    [InlineData("analysis.cancel")]
    public async Task LockedRepositoryAllowsEveryUnclassifiedOrAnalysisObservationAction(string action)
    {
        await using var fixture = await EngineHostTestFixture.CreateBlockingPipelineAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync(new AnalysisCheckSelection(false, false));

        var response = await fixture.SendRealProtocolRequestAsync(action, fixture.RequestParametersFor(action, runId));

        fixture.AssertNotBusyError(response);
        fixture.ReleaseBlockingPipeline();
        await fixture.Host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Asynchronously verifies a noncanonical retained repository ID reaches the handler's invalid-request check.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task LockedRepositoryPreservesInvalidRequestForNoncanonicalRepositoryId()
    {
        await using var fixture = await EngineHostTestFixture.CreateBlockingPipelineAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        await fixture.AcceptRunAsync(new AnalysisCheckSelection(false, false));
        var parameters = JsonSerializer.SerializeToElement(new { repositoryId = fixture.RepositoryId.ToString("B") });

        var response = await fixture.SendRealProtocolRequestAsync("repositories.removeRecent", parameters);

        var errorResponse = Assert.IsType<ProtocolErrorResponse>(response);
        var error = Assert.Single(errorResponse.Errors);
        Assert.Equal("protocol.invalidRequest", error.Code);
        fixture.ReleaseBlockingPipeline();
        await fixture.Host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Asynchronously verifies repository-history failures retain their complete error identity.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RepositoryHistoryFailureIsPropagatedLosslessly()
    {
        var expectedError = OperationError.ExternalDependencyFailure(
            "The controlled repository history is unavailable.",
            LocalStateErrorCode.Unavailable);
        var guard = new RepositoryBusyGuard(
            null!,
            new StubRepositoryHistoryStore(Result.Fail<RepositoryHistorySnapshot>(expectedError)),
            null!,
            null!);

        var result = await guard.CheckRepositoryIdAsync(Guid.NewGuid().ToString("D"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Same(expectedError, Assert.Single(result.Errors));
    }

    /// <summary>
    ///     Asynchronously verifies the matrix never reports a reservation conflict without an active run.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task EveryActionSucceedsForAnUnlockedRepository()
    {
        await using var fixture = await EngineHostTestFixture.CreateAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = Guid.NewGuid();

        foreach (var action in fixture.EveryRepositoryScopedAction())
        {
            var response = await fixture.SendRealProtocolRequestAsync(action, fixture.RequestParametersFor(action, runId));
            fixture.AssertNotBusyError(response);
        }

        await fixture.Host.StopAsync(TestContext.Current.CancellationToken);
    }
}
