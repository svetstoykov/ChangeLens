using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Engine.IntegrationTests.Analysis.Support;
using ChangeLens.Engine.IntegrationTests.Hosting.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis;

/// <summary>Verifies committed snapshot capture through the production analysis pipeline and SQLite state store.</summary>
public sealed class CapturePipelineTests
{
    [Fact]
    public async Task CaptureSucceedsThenRunCompletes()
    {
        var captureService = new BlockingSnapshotCaptureService { OutcomeFactory = run => Result.Success(CreateCapture(run, 0)) };
        await using var fixture = await EngineHostTestFixture.CreateWithSnapshotCaptureAsync(captureService);
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync();
        await captureService.Entered.WaitAsync(TestContext.Current.CancellationToken);
        captureService.Release();

        var detail = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(5));

        Assert.Equal(AnalysisRunState.Completed, detail.State);
        Assert.NotNull(detail.CapturedAtUnixMilliseconds);
        Assert.NotNull(detail.SnapshotId);
        Assert.Equal(1, detail.CapturedChangedFileCount);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CaptureWithExcludedWorkCompletesWithLimitations()
    {
        var captureService = new BlockingSnapshotCaptureService { OutcomeFactory = run => Result.Success(CreateCapture(run, 2)) };
        await using var fixture = await EngineHostTestFixture.CreateWithSnapshotCaptureAsync(captureService);
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync();
        await captureService.Entered.WaitAsync(TestContext.Current.CancellationToken);
        captureService.Release();

        var detail = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(5));

        Assert.Equal(AnalysisRunState.CompletedWithLimitations, detail.State);
        Assert.Equal(1, detail.Terminal!.LimitationCount);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CaptureFailureStopsThePipelineWithItsCode()
    {
        var captureService = new BlockingSnapshotCaptureService
        {
            Outcome = Result.Fail<SnapshotCapture>(OperationError.Validation(
                "The accepted HEAD revision moved before capture.", AnalysisFailureCode.StaleAtCapture)),
        };
        await using var fixture = await EngineHostTestFixture.CreateWithSnapshotCaptureAsync(captureService);
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync();
        await captureService.Entered.WaitAsync(TestContext.Current.CancellationToken);
        captureService.Release();

        var detail = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(5));

        Assert.Equal(AnalysisRunState.Failed, detail.State);
        Assert.Equal(AnalysisFailureCode.StaleAtCapture, detail.Terminal!.FailureCode);
        Assert.Null(detail.SnapshotId);
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CancellationBeforeCommitLeavesNoSnapshot()
    {
        var captureService = new BlockingSnapshotCaptureService { OutcomeFactory = run => Result.Success(CreateCapture(run, 0)) };
        await using var fixture = await EngineHostTestFixture.CreateWithSnapshotCaptureAsync(captureService);
        await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
        var runId = await fixture.AcceptRunAsync();
        await captureService.Entered.WaitAsync(TestContext.Current.CancellationToken);

        await fixture.RequestCancellationAsync(runId);
        captureService.Release();

        var detail = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(5));

        Assert.Equal(AnalysisRunState.Cancelled, detail.State);
        Assert.Null(detail.CapturedAtUnixMilliseconds);
        Assert.Null(detail.SnapshotId);
        Assert.Equal(0, await fixture.CountManifestEntriesAsync(runId));
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CancellationAfterCommitKeepsTheSnapshot()
    {
        var captureService = new BlockingSnapshotCaptureService { OutcomeFactory = run => Result.Success(CreateCapture(run, 0)) };
        EngineHostTestFixture? fixture = null;
        fixture = await EngineHostTestFixture.CreateWithCaptureCommitObserverAsync(captureService,
            committedRunId => fixture!.RequestCancellationAsync(committedRunId));
        try
        {
            await fixture.Host.StartAsync(TestContext.Current.CancellationToken);
            var runId = await fixture.AcceptRunAsync();
            await captureService.Entered.WaitAsync(TestContext.Current.CancellationToken);
            captureService.Release();

            var detail = await fixture.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(5));

            Assert.Equal(AnalysisRunState.Cancelled, detail.State);
            Assert.NotNull(detail.CapturedAtUnixMilliseconds);
            Assert.NotNull(detail.SnapshotId);
            Assert.Equal(1, await fixture.CountManifestEntriesAsync(runId));
            await fixture.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    private static SnapshotCapture CreateCapture(AnalysisRunDetail run, int excludedTotal)
    {
        var entry = new SnapshotManifestEntry("captured.txt", null, SnapshotChangeCategory.Modified, "100644", "100644",
            new string('a', 40), new string('b', 40));
        var manifest = new SnapshotManifest(Guid.NewGuid(), new string('c', 64), run.Repository.CanonicalRepositoryPathKey,
            run.Comparison.Target, run.Comparison.TargetRevision, run.Repository.HeadRevision, new string('d', 40), [entry]);
        return new SnapshotCapture(manifest, new ExcludedUncommittedCounts(excludedTotal, excludedTotal, 0, 0, 0));
    }
}
