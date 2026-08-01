using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Engine.IntegrationTests.Hosting.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Hosting;

public sealed class AnalysisProcessorHostingTests
{
    [Fact]
    public async Task StartupRecoveryCompletesBeforeProtocolHostReadsRequests()
    {
        await using var seedFixture = await EngineHostTestFixture.CreateAsync();
        var orphanedRunId = await seedFixture.SeedActiveRunLeftByAPreviousProcessAsync();
        await using var fixture = await EngineHostTestFixture.ReopenAsync(seedFixture, orphanedRunId);

        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        await fixture.WaitForProtocolReadAsync(TestContext.Current.CancellationToken);
        var projection = await fixture.PollOnceAsync(orphanedRunId);

        Assert.Equal(AnalysisRunState.Interrupted, projection.State);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ClaimedRunReachesCompletedWithoutLimitations()
    {
        await using var fixture = await EngineHostTestFixture.CreateAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync();

        var projection = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(5));

        Assert.Equal(AnalysisRunState.Completed, projection.State);
        Assert.Null(projection.Terminal!.LimitationCount);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LostWakeUpSignalIsRecoveredByDatabaseFallback()
    {
        await using var fixture = await EngineHostTestFixture.CreateAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunWithoutSignalingAsync();

        var projection = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(3));

        Assert.Equal(AnalysisRunState.Completed, projection.State);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnePipelineFailureDoesNotStopLaterRuns()
    {
        await using var fixture = await EngineHostTestFixture.CreateFailingFirstRunAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var failingRunId = await fixture.AcceptRunAsync();
        var failingProjection = await fixture.PollUntilTerminalAsync(failingRunId, TimeSpan.FromSeconds(5));
        var laterRunId = await fixture.AcceptRunAsync();

        var laterProjection = await fixture.PollUntilTerminalAsync(laterRunId, TimeSpan.FromSeconds(5));

        Assert.Equal(AnalysisRunState.Failed, failingProjection.State);
        Assert.Equal("analysis.unexpectedFailure", failingProjection.Terminal!.FailureCode);
        Assert.Equal(AnalysisRunState.Completed, laterProjection.State);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RowsLeftActiveByAPreviousProcessBecomeInterruptedBeforeRequestsAreRead()
    {
        await using var seedFixture = await EngineHostTestFixture.CreateAsync();
        var orphanedRunId = await seedFixture.SeedActiveRunLeftByAPreviousProcessAsync();

        await using var fixture = await EngineHostTestFixture.ReopenAsync(seedFixture);
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var projection = await fixture.PollUntilAsync(
            orphanedRunId,
            candidate => candidate.State == AnalysisRunState.Interrupted,
            TimeSpan.FromSeconds(3));

        Assert.Equal("engineStopped", projection.InterruptionReason);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessorRespectsConfiguredMaximumConcurrentRunsOfOne()
    {
        await using var fixture = await EngineHostTestFixture.CreateBlockingPipelineAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var firstRunId = await fixture.AcceptRunAsync();
        await fixture.WaitUntilClaimedAsync(firstRunId, TimeSpan.FromSeconds(3));
        var secondRunId = await fixture.AcceptRunForAnotherRepositoryAsync();

        var secondProjection = await fixture.PollOnceAsync(secondRunId);

        Assert.Equal(AnalysisRunState.PendingCapture, secondProjection.State);
        fixture.ReleaseBlockingPipeline();
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InFlightUserCancellationCommitsCancelledInsteadOfUnexpectedFailure()
    {
        await using var fixture = await EngineHostTestFixture.CreateCancellationAwarePipelineAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync();
        await fixture.WaitUntilPipelineStartsAsync(TestContext.Current.CancellationToken);

        await fixture.RequestCancellationAsync(runId);
        var projection = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(3));

        Assert.Equal(AnalysisRunState.Cancelled, projection.State);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ShutdownDuringAnInFlightRunDoesNotCommitUnexpectedFailure()
    {
        await using var fixture = await EngineHostTestFixture.CreateBlockingPipelineAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync();
        await fixture.WaitUntilClaimedAsync(runId, TimeSpan.FromSeconds(3));

        await fixture.StopAsync(TestContext.Current.CancellationToken);
        var projection = await fixture.PollOnceAsync(runId);

        Assert.Equal(AnalysisRunState.Capturing, projection.State);
        Assert.Null(projection.Terminal);
    }
}
