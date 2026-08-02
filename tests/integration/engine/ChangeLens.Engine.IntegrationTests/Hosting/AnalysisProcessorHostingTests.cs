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
        var detail = await fixture.PollOnceAsync(orphanedRunId);

        Assert.Equal(AnalysisRunState.Interrupted, detail.State);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TakenRunReachesCompletedWithoutLimitations()
    {
        await using var fixture = await EngineHostTestFixture.CreateAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync();

        var detail = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(5));

        Assert.Equal(AnalysisRunState.Completed, detail.State);
        Assert.Null(detail.Terminal!.LimitationCount);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LostWakeUpSignalIsRecoveredByDatabaseFallback()
    {
        await using var fixture = await EngineHostTestFixture.CreateAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunWithoutSignalingAsync();

        var detail = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(3));

        Assert.Equal(AnalysisRunState.Completed, detail.State);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnePipelineFailureDoesNotStopLaterRuns()
    {
        await using var fixture = await EngineHostTestFixture.CreateFailingFirstRunAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var failingRunId = await fixture.AcceptRunAsync();
        var failingDetail = await fixture.PollUntilTerminalAsync(failingRunId, TimeSpan.FromSeconds(5));
        var laterRunId = await fixture.AcceptRunAsync();

        var laterDetail = await fixture.PollUntilTerminalAsync(laterRunId, TimeSpan.FromSeconds(5));

        Assert.Equal(AnalysisRunState.Failed, failingDetail.State);
        Assert.Equal("analysis.unexpectedFailure", failingDetail.Terminal!.FailureCode);
        Assert.Equal(AnalysisRunState.Completed, laterDetail.State);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RowsLeftActiveByAPreviousProcessBecomeInterruptedBeforeRequestsAreRead()
    {
        await using var seedFixture = await EngineHostTestFixture.CreateAsync();
        var orphanedRunId = await seedFixture.SeedActiveRunLeftByAPreviousProcessAsync();

        await using var fixture = await EngineHostTestFixture.ReopenAsync(seedFixture);
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var detail = await fixture.PollUntilAsync(
            orphanedRunId,
            candidate => candidate.State == AnalysisRunState.Interrupted,
            TimeSpan.FromSeconds(3));

        Assert.Equal("engineStopped", detail.InterruptionReason);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessorRespectsConfiguredMaximumConcurrentRunsOfOne()
    {
        await using var fixture = await EngineHostTestFixture.CreateBlockingPipelineAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var firstRunId = await fixture.AcceptRunAsync();
        await fixture.WaitUntilTakenAsync(firstRunId, TimeSpan.FromSeconds(3));
        var secondRunId = await fixture.AcceptRunForAnotherRepositoryAsync();

        var secondDetail = await fixture.PollOnceAsync(secondRunId);

        Assert.Equal(AnalysisRunState.PendingCapture, secondDetail.State);
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
        var detail = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(3));

        Assert.Equal(AnalysisRunState.Cancelled, detail.State);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ShutdownDuringAnInFlightRunDoesNotCommitUnexpectedFailure()
    {
        await using var fixture = await EngineHostTestFixture.CreateBlockingPipelineAsync();
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync();
        await fixture.WaitUntilTakenAsync(runId, TimeSpan.FromSeconds(3));

        await fixture.StopAsync(TestContext.Current.CancellationToken);
        var detail = await fixture.PollOnceAsync(runId);

        Assert.Equal(AnalysisRunState.Capturing, detail.State);
        Assert.Null(detail.Terminal);
    }
}
