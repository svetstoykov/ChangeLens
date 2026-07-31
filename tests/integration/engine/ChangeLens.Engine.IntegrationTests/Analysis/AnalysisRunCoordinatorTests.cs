using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Services;
using ChangeLens.Engine.IntegrationTests.Analysis.Support;
using ChangeLens.Engine.IntegrationTests.Comparisons.Handlers.Support;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.LocalState.Services;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis;

/// <summary>Verifies analysis-run coordination across freshness, durable state, and processor control.</summary>
public sealed class AnalysisRunCoordinatorTests
{
    [Fact]
    public async Task StartWithStaleFreshnessReturnsRejectedStaleWithoutPersistence()
    {
        var coordinator = CreateCoordinator(
            new StubAnalysisRunStore(),
            new AnalysisProcessorControl(),
            new ComparisonFreshnessCheck(ComparisonFreshnessState.Stale, null, null));

        var outcome = await coordinator.StartAsync(
            "/repository",
            "refs/heads/main",
            new string('0', 64),
            new AnalysisCheckSelection(true, true),
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisStartOutcomeKind.RejectedStale, outcome.Data!.Kind);
    }

    [Fact]
    public async Task TestsWithoutBuildIsRejectedAsValidation()
    {
        var coordinator = CreateCoordinator(
            new StubAnalysisRunStore(),
            new AnalysisProcessorControl(),
            new ComparisonFreshnessCheck(ComparisonFreshnessState.Stale, null, null));

        var outcome = await coordinator.StartAsync(
            "/repository",
            "refs/heads/main",
            new string('0', 64),
            new AnalysisCheckSelection(false, true),
            null,
            TestContext.Current.CancellationToken);

        Assert.True(outcome.IsFailure);
        Assert.Equal("analysis.testsRequireBuild", outcome.Errors[0].Code);
    }

    [Fact]
    public async Task AcceptedStartSignalsProcessor()
    {
        var processor = new AnalysisProcessorControl();
        var waitForSignal = processor.WaitForPendingWorkAsync(TestContext.Current.CancellationToken);
        var store = new StubAnalysisRunStore(create: (_, _) => Task.FromResult<Result<AnalysisStartOutcome>>(
            new AnalysisStartOutcome(
                AnalysisStartOutcomeKind.Accepted,
                Guid.Parse("0198a1b2-3c4d-4e5f-8a9b-0123456789ab"),
                42,
                null)));
        var coordinator = CreateCoordinator(store, processor, CreateCurrentFreshness());

        var outcome = await coordinator.StartAsync(
            "/repository",
            "refs/heads/main",
            new string('0', 64),
            new AnalysisCheckSelection(true, false),
            null,
            TestContext.Current.CancellationToken);

        await waitForSignal;
        Assert.Equal(AnalysisStartOutcomeKind.Accepted, outcome.Data!.Kind);
    }

    [Fact]
    public async Task CancelUnknownRunReturnsUnknownRunError()
    {
        var store = new StubAnalysisRunStore(requestCancellation: (_, _, _) => Task.FromResult(
            Result.ErrorFromResult<AnalysisRunDetail>(Result.Fail(
                OperationError.NotFound("No analysis run matches the supplied identifier.", "analysis.unknownRun")))));
        var coordinator = CreateCoordinator(store, new AnalysisProcessorControl(), CreateCurrentFreshness());

        var result = await coordinator.CancelAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("analysis.unknownRun", result.Errors[0].Code);
    }

    [Fact]
    public async Task CancelInterruptsTheRunCurrentlyExecutingInThisProcess()
    {
        var processor = new AnalysisProcessorControl();
        var runId = Guid.NewGuid();
        var store = new StubAnalysisRunStore(requestCancellation: (_, _, _) => Task.FromResult<Result<AnalysisRunDetail>>(CreateDetail(runId)));
        var coordinator = CreateCoordinator(store, processor, CreateCurrentFreshness());
        var runToken = processor.BeginRun(runId);

        await coordinator.CancelAsync(runId, TestContext.Current.CancellationToken);

        Assert.True(runToken.IsCancellationRequested);
    }

    [Fact]
    public async Task CancelDoesNotInterruptARunThatIsNotExecuting()
    {
        var processor = new AnalysisProcessorControl();
        var executingRunId = Guid.NewGuid();
        var otherRunId = Guid.NewGuid();
        var store = new StubAnalysisRunStore(requestCancellation: (_, _, _) => Task.FromResult<Result<AnalysisRunDetail>>(CreateDetail(otherRunId)));
        var coordinator = CreateCoordinator(store, processor, CreateCurrentFreshness());
        var runToken = processor.BeginRun(executingRunId);

        await coordinator.CancelAsync(otherRunId, TestContext.Current.CancellationToken);

        Assert.False(runToken.IsCancellationRequested);
    }

    private static AnalysisRunCoordinator CreateCoordinator(
        StubAnalysisRunStore store,
        AnalysisProcessorControl processor,
        ComparisonFreshnessCheck freshness)
    {
        return new AnalysisRunCoordinator(
            store,
            new StubGitComparisonFreshnessChecker(freshness),
            new PhysicalRepositoryPathResolver(),
            new CanonicalRepositoryPathKeyProvider(),
            processor,
            TimeProvider.System);
    }

    private static ComparisonFreshnessCheck CreateCurrentFreshness() => new(
        ComparisonFreshnessState.Current,
        new RepositoryDescriptor(
            "change_lens",
            "/repository",
            new BranchRepositoryHead("main", "0123456789abcdef0123456789abcdef01234567")),
        "89abcdef0123456789abcdef0123456789abcdef");

    private static AnalysisRunDetail CreateDetail(Guid runId) => new(
        runId,
        AnalysisRunState.PendingCapture,
        new AnalysisRepositoryIdentity(
            Guid.Parse("5298a1b2-3c4d-4e5f-8a9b-0123456789ab"),
            "change_lens",
            "/repository",
            "0123456789abcdef0123456789abcdef01234567"),
        new AnalysisComparisonIdentity(
            "refs/heads/main",
            "89abcdef0123456789abcdef0123456789abcdef",
            new string('0', 64)),
        new AnalysisCheckSelection(true, false),
        42,
        null,
        false,
        null,
        null,
        null);
}
