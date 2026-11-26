using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.AnalysisRuns.Services;
using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.IntegrationTests.AnalysisRuns.Support;
using ChangeLens.Engine.IntegrationTests.Hosting.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Infrastructure.Repositories.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.AnalysisRuns;

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
        await fixture.AcceptRunAsync();

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
        var runId = await fixture.AcceptRunAsync();

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
        await fixture.AcceptRunAsync();
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
            new StubAnalysisRunStore(),
            new StubRepositoryHistoryStore(Result.Fail<RepositoryHistorySnapshot>(expectedError)),
            new PhysicalRepositoryPathResolver(),
            new CanonicalRepositoryPathKeyProvider(),
            NullLogger<RepositoryBusyGuard>.Instance);

        var result = await guard.CheckRepositoryIdAsync(Guid.NewGuid().ToString("D"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Same(expectedError, Assert.Single(result.Errors));
    }

    /// <summary>
    ///     Asynchronously verifies a reservation conflict is logged with the blocking run and stable error code.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ReservationConflictIsLoggedWithTheActiveRun()
    {
        var repositoryId = Guid.NewGuid();
        var activeRunId = Guid.NewGuid();
        var activeRun = new AnalysisRunDetail(
            activeRunId, AnalysisRunState.Capturing,
            new AnalysisRepositoryIdentity(repositoryId, "fixture", "/fixture/repo", "/fixture/repo", "head"),
            new AnalysisComparisonIdentity("main", "target", "token"),
            null, 1, null, null, null, null, null, null, null, null, null, false, null, null, null);
        var logger = new RecordingLogger<RepositoryBusyGuard>();
        var guard = new RepositoryBusyGuard(
            new StubAnalysisRunStore(getActive: _ => Result.Success<AnalysisRunDetail?>(activeRun)),
            new StubRepositoryHistoryStore(Result.Success(new RepositoryHistorySnapshot(
                repositoryId, [new RepositoryHistoryEntry(repositoryId, "/fixture/repo", "fixture", 1, null)]))),
            new PhysicalRepositoryPathResolver(),
            new CanonicalRepositoryPathKeyProvider(),
            logger);

        var result = await guard.CheckRepositoryIdAsync(repositoryId.ToString("D"), TestContext.Current.CancellationToken);

        Assert.Equal(RepositoryErrorCode.Busy, Assert.Single(result.Errors).Code);
        Assert.Contains(logger.Entries, entry => entry.Level >= LogLevel.Information
            && entry.Message.Contains(activeRunId.ToString(), StringComparison.Ordinal)
            && entry.Message.Contains(RepositoryErrorCode.Busy, StringComparison.Ordinal)
            && entry.Message.EndsWith(" ms.", StringComparison.Ordinal));
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
