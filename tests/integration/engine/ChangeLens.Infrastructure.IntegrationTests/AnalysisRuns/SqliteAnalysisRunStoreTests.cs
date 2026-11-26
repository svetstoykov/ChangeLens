using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns.Support;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns;

/// <summary>
///     Verifies <see cref="ChangeLens.Infrastructure.AnalysisRuns.Services.SqliteAnalysisRunStore" /> acceptance and
///     processor-take behavior against a real SQLite database.
/// </summary>
public sealed class SqliteAnalysisRunStoreTests
{
    /// <summary>
    ///     Verifies that two acceptance requests racing for the same repository produce exactly one accepted
    ///     pending row and one rejected-active outcome.
    /// </summary>
    [Fact]
    public async Task SameRepositoryRacingAcceptanceProducesOnePendingRowAndOneRejectedActive()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var acceptanceA = fixture.Acceptance();
        var acceptanceB = fixture.Acceptance();

        var resultA = await fixture.Store.CreateOrReturnActiveAsync(acceptanceA, TestContext.Current.CancellationToken);
        var resultB = await fixture.Store.CreateOrReturnActiveAsync(acceptanceB, TestContext.Current.CancellationToken);

        Assert.True(resultA.IsSuccess);
        Assert.True(resultB.IsSuccess);
        var outcomes = new[] { resultA.Data!.Kind, resultB.Data!.Kind };
        Assert.Contains(AnalysisStartOutcomeKind.Accepted, outcomes);
        Assert.Contains(AnalysisStartOutcomeKind.RejectedActive, outcomes);
    }

    /// <summary>
    ///     Verifies that acceptance requests for two different repositories are both accepted without either
    ///     rejecting the other.
    /// </summary>
    [Fact]
    public async Task DifferentRepositoriesAreBothAcceptedSimultaneously()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var first = await fixture.SeedRepositoryAsync("/repository-one");
        var second = await fixture.SeedRepositoryAsync("/repository-two");

        var resultA = await fixture.Store.CreateOrReturnActiveAsync(
            fixture.Acceptance("/repository-one"),
            TestContext.Current.CancellationToken);
        var resultB = await fixture.Store.CreateOrReturnActiveAsync(
            fixture.Acceptance("/repository-two"),
            TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisStartOutcomeKind.Accepted, resultA.Data!.Kind);
        Assert.Equal(AnalysisStartOutcomeKind.Accepted, resultB.Data!.Kind);
    }

    /// <summary>
    ///     Verifies that taking the next pending run selects deterministically by requested-at timestamp then
    ///     run identifier.
    /// </summary>
    [Fact]
    public async Task TakeIsDeterministicByRequestedAtThenRunId()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        await fixture.SeedRepositoryAsync("/repository-a");
        await fixture.SeedRepositoryAsync("/repository-b");
        var earlier = fixture.Acceptance("/repository-a", requestedAtUnixMilliseconds: 1_000);
        var later = fixture.Acceptance("/repository-b", requestedAtUnixMilliseconds: 2_000);
        await fixture.Store.CreateOrReturnActiveAsync(later, TestContext.Current.CancellationToken);
        await fixture.Store.CreateOrReturnActiveAsync(earlier, TestContext.Current.CancellationToken);

        var take = await fixture.Store.TakeNextPendingAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(take.Data);
        var takenRun = await fixture.GetRunAsync(take.Data!.Value);
        Assert.Equal(1_000, takenRun.RequestedAtUnixMilliseconds);
    }

    /// <summary>
    ///     Verifies that taking the next pending run transitions the selected row to the capturing state.
    /// </summary>
    [Fact]
    public async Task TakeTransitionsTheRowToCapturing()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        await fixture.Store.CreateOrReturnActiveAsync(
            fixture.Acceptance(),
            TestContext.Current.CancellationToken);

        var take = await fixture.Store.TakeNextPendingAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(take.Data);
        var takenRun = await fixture.GetRunAsync(take.Data!.Value);
        Assert.Equal(AnalysisRunState.Capturing, takenRun.State);
        Assert.NotNull(takenRun.CaptureStartedAtUnixMilliseconds);
    }

    /// <summary>
    ///     Verifies that a taken run's step plan, step begin/finish outcomes, and stage transition all persist
    ///     durably in one coordinated sequence.
    /// </summary>
    [Fact]
    public async Task StepPlanBeginFinishAndStageTransitionPersist()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var take = await fixture.Store.TakeNextPendingAsync(TestContext.Current.CancellationToken);
        var plan = new[]
        {
            new AnalysisRunStepPlanEntry("analysis.lifecycle.capture", "engine", "lifecycle", 0, AnalysisStage.Capturing),
        };

        await fixture.Store.EstablishStepPlanAsync(runId, plan, TestContext.Current.CancellationToken);
        await fixture.Store.BeginStepAsync(runId, "analysis.lifecycle.capture", 2_000, TestContext.Current.CancellationToken);
        var finish = await fixture.Store.FinishStepAsync(
            runId,
            new AnalysisRunStepOutcome("analysis.lifecycle.capture", AnalysisRunStepState.Succeeded, null),
            3_000,
            TestContext.Current.CancellationToken);
        var stageResult = await fixture.Store.TransitionStageAsync(
            runId,
            AnalysisRunState.Capturing,
            AnalysisRunState.Discovering,
            3_000,
            TestContext.Current.CancellationToken);

        Assert.NotNull(take.Data);
        Assert.True(finish.Data);
        Assert.Equal(AnalysisRunState.Discovering, stageResult.Data);
        var step = await fixture.GetStepAsync(runId, "analysis.lifecycle.capture");
        Assert.Equal(AnalysisRunStepState.Succeeded, step.State);
    }

    /// <summary>
    ///     Verifies that finishing a step that is not currently <see cref="AnalysisRunStepState.Running" /> is
    ///     rejected without mutating the step, so a duplicate or out-of-order finish call cannot silently overwrite
    ///     an already-finished step.
    /// </summary>
    [Fact]
    public async Task FinishStepRejectsWhenStepIsNotRunning()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var plan = new[]
        {
            new AnalysisRunStepPlanEntry("analysis.lifecycle.capture", "engine", "lifecycle", 0, AnalysisStage.Capturing),
        };
        await fixture.Store.EstablishStepPlanAsync(runId, plan, TestContext.Current.CancellationToken);
        await fixture.Store.BeginStepAsync(runId, "analysis.lifecycle.capture", 2_000, TestContext.Current.CancellationToken);
        var firstFinish = await fixture.Store.FinishStepAsync(
            runId,
            new AnalysisRunStepOutcome("analysis.lifecycle.capture", AnalysisRunStepState.Succeeded, null),
            3_000,
            TestContext.Current.CancellationToken);

        var duplicateFinish = await fixture.Store.FinishStepAsync(
            runId,
            new AnalysisRunStepOutcome("analysis.lifecycle.capture", AnalysisRunStepState.Failed, "some.error"),
            9_000,
            TestContext.Current.CancellationToken);

        Assert.True(firstFinish.Data);
        Assert.False(duplicateFinish.Data);
        var step = await fixture.GetStepAsync(runId, "analysis.lifecycle.capture");
        Assert.Equal(AnalysisRunStepState.Succeeded, step.State);
        Assert.Equal(3_000, step.FinishedAtUnixMilliseconds);
    }

    /// <summary>
    ///     Verifies that requesting cancellation twice persists only the first request timestamp and both calls
    ///     report cancellation as requested.
    /// </summary>
    [Fact]
    public async Task CancellationPersistsOnceAndIsIdempotent()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();

        var first = await fixture.Store.RequestCancellationAsync(runId, 5_000, TestContext.Current.CancellationToken);
        var second = await fixture.Store.RequestCancellationAsync(runId, 9_000, TestContext.Current.CancellationToken);

        Assert.True(first.Data!.CancellationRequested);
        Assert.True(second.Data!.CancellationRequested);
        var run = await fixture.GetRunAsync(runId);
        Assert.Equal(5_000, run.CancellationRequestedAtUnixMilliseconds);
    }

    /// <summary>
    ///     Verifies that only the first <see cref="ChangeLens.Core.AnalysisRuns.Interfaces.IAnalysisRunStore.CommitTerminalAsync" />
    ///     call for a run commits its terminal state, and a later call for the same run has no effect.
    /// </summary>
    [Fact]
    public async Task TerminalTransitionCommitsExactlyOnce()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Completed, 10_000, null, null);

        var firstCommit = await fixture.Store.CommitTerminalAsync(runId, terminal, null, TestContext.Current.CancellationToken);
        var secondCommit = await fixture.Store.CommitTerminalAsync(
            runId,
            terminal with { Kind = AnalysisTerminalKind.Failed, FailureCode = "analysis.unexpectedFailure" },
            null,
            TestContext.Current.CancellationToken);

        Assert.True(firstCommit.Data);
        Assert.False(secondCommit.Data);
        var run = await fixture.GetRunAsync(runId);
        Assert.Equal(AnalysisRunState.Completed, run.State);
    }

    /// <summary>
    ///     Verifies that a limited terminal state and its limitation count survive a durable round trip, so the
    ///     outcome remains representable before any contributor produces a real limitation.
    /// </summary>
    [Fact]
    public async Task LimitedTerminalStateRoundTripsWithItsLimitationCount()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.CompletedWithLimitations, 10_000, 2, null);

        var commit = await fixture.Store.CommitTerminalAsync(runId, terminal, null, TestContext.Current.CancellationToken);
        var detail = await fixture.Store.GetDetailAsync(runId, TestContext.Current.CancellationToken);

        Assert.True(commit.Data);
        Assert.Equal(AnalysisRunState.CompletedWithLimitations, detail.Data!.State);
        Assert.Equal(AnalysisTerminalKind.CompletedWithLimitations, detail.Data.Terminal!.Kind);
        Assert.Equal(2, detail.Data.Terminal.LimitationCount);
    }

    /// <summary>
    ///     Verifies that finalizing cancelled pending runs commits the durable cancelled terminal state without
    ///     taking the run, and that the finalized run is no longer takeable.
    /// </summary>
    [Fact]
    public async Task FinalizesCancelledPendingRunsWithoutTakingThem()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        await fixture.Store.RequestCancellationAsync(runId, 4_000, TestContext.Current.CancellationToken);

        var finalizedCount = await fixture.Store.FinalizeCancelledPendingRunsAsync(
            6_000,
            TestContext.Current.CancellationToken);
        var take = await fixture.Store.TakeNextPendingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, finalizedCount.Data);
        Assert.Null(take.Data);
        var run = await fixture.GetRunAsync(runId);
        Assert.Equal(AnalysisRunState.Cancelled, run.State);
        Assert.Equal(6_000, run.TerminalAtUnixMilliseconds);
    }

    /// <summary>
    ///     Verifies that startup recovery interrupts every active row and releases each repository reservation.
    /// </summary>
    [Fact]
    public async Task StartupInterruptionMarksEveryActiveRowAndReleasesItsReservation()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var orphanedRunId = await fixture.CreateAcceptedRunAsync();
        await fixture.SeedRepositoryAsync("/repository-second");
        await fixture.Store.CreateOrReturnActiveAsync(
            fixture.Acceptance("/repository-second"),
            TestContext.Current.CancellationToken);

        var interruptedCount = await fixture.Store.InterruptActiveRunsAsync(
            20_000,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, interruptedCount.Data);
        var orphanedRun = await fixture.GetRunAsync(orphanedRunId);
        Assert.Equal(AnalysisRunState.Interrupted, orphanedRun.State);
        Assert.Equal(20_000, orphanedRun.InterruptedAtUnixMilliseconds);
        Assert.Equal(AnalysisInterruptionReason.EngineStopped, orphanedRun.InterruptionReason);

        var reaccepted = await fixture.Store.CreateOrReturnActiveAsync(
            fixture.Acceptance(),
            TestContext.Current.CancellationToken);
        Assert.Equal(AnalysisStartOutcomeKind.Accepted, reaccepted.Data!.Kind);
    }

    /// <summary>
    ///     Verifies that reading the active run for a repository returns the detail for its active run when one
    ///     exists at the supplied canonical path key.
    /// </summary>
    [Fact]
    public async Task GetActiveByRepositoryReturnsActiveRunDetail()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        await fixture.SeedRepositoryAsync("/repository-active");
        var acceptance = fixture.Acceptance("/repository-active");
        var outcome = await fixture.Store.CreateOrReturnActiveAsync(acceptance, TestContext.Current.CancellationToken);
        var runId = outcome.Data!.RunId!.Value;

        var active = await fixture.Store.GetActiveByRepositoryAsync("/repository-active", TestContext.Current.CancellationToken);

        Assert.NotNull(active.Data);
        Assert.Equal(runId, active.Data!.RunId);
        Assert.Equal(AnalysisRunState.PendingCapture, active.Data!.State);
    }

    /// <summary>
    ///     Verifies that reading the active run for a repository with no active run returns a successful result
    ///     carrying a <see langword="null" /> detail.
    /// </summary>
    [Fact]
    public async Task GetActiveByRepositoryReturnsNullWhenNoActiveRunExists()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        await fixture.SeedRepositoryAsync("/repository-idle");

        var active = await fixture.Store.GetActiveByRepositoryAsync("/repository-idle", TestContext.Current.CancellationToken);

        Assert.True(active.IsSuccess);
        Assert.Null(active.Data);
    }

    /// <summary>
    ///     Verifies that every <see cref="ChangeLens.Core.AnalysisRuns.Interfaces.IAnalysisRunStore" /> operation converts
    ///     expected SQLite access failures into <c>localState.unavailable</c> instead of throwing.
    /// </summary>
    [Fact]
    public async Task AllOperationsReturnUnavailableWhenSqliteTablesAreMissing()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var capture = fixture.Capture(runId, 1, 0);
        var terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Completed, 5_000, null, null);
        var plan = new[]
        {
            new AnalysisRunStepPlanEntry("analysis.lifecycle.capture", "engine", "lifecycle", 0, AnalysisStage.Capturing),
        };
        var stepOutcome = new AnalysisRunStepOutcome("analysis.lifecycle.capture", AnalysisRunStepState.Succeeded, null);
        var cancellationToken = TestContext.Current.CancellationToken;

        await fixture.Context.Database.ExecuteSqlRawAsync(
            """
            DROP TABLE snapshot_manifest_entries;
            DROP TABLE analysis_run_steps;
            DROP TABLE analysis_runs;
            """,
            cancellationToken);

        Result[] nonGenericResults =
        [
            await fixture.Store.EstablishStepPlanAsync(runId, plan, cancellationToken),
            await fixture.Store.BeginStepAsync(runId, "analysis.lifecycle.capture", 2_000, cancellationToken),
            await fixture.Store.RecordCorrespondenceCandidateCountAsync(runId, 3, cancellationToken),
            await fixture.Store.RecordDisclosedEvidenceNodeCountAsync(runId, 4, cancellationToken),
            await fixture.Store.RecordValidationRemovalCountAsync(runId, 1, cancellationToken),
        ];
        foreach (var result in nonGenericResults)
        {
            AssertUnavailable(result);
        }

        AssertUnavailable(await fixture.Store.CreateOrReturnActiveAsync(fixture.Acceptance(), cancellationToken));
        AssertUnavailable(await fixture.Store.GetDetailAsync(runId, cancellationToken));
        AssertUnavailable(await fixture.Store.GetActiveByRepositoryAsync(AnalysisRunStoreTestFixture.DefaultCanonicalPath, cancellationToken));
        AssertUnavailable(await fixture.Store.TakeNextPendingAsync(cancellationToken));
        AssertUnavailable(await fixture.Store.TransitionStageAsync(
            runId, AnalysisRunState.Capturing, AnalysisRunState.Discovering, 2_000, cancellationToken));
        AssertUnavailable(await fixture.Store.FinishStepAsync(runId, stepOutcome, 3_000, cancellationToken));
        AssertUnavailable(await fixture.Store.RequestCancellationAsync(runId, 4_000, cancellationToken));
        AssertUnavailable(await fixture.Store.CommitCaptureAsync(runId, capture, 2_500, cancellationToken));
        AssertUnavailable(await fixture.Store.CommitTerminalAsync(runId, terminal, null, cancellationToken));
        AssertUnavailable(await fixture.Store.GetReadingProjectionAsync(runId, cancellationToken));
        AssertUnavailable(await fixture.Store.FinalizeCancelledPendingRunsAsync(6_000, cancellationToken));
        AssertUnavailable(await fixture.Store.InterruptActiveRunsAsync(7_000, cancellationToken));
    }

    /// <summary>
    ///     Verifies that caller cancellation propagates as <see cref="OperationCanceledException" /> rather than being
    ///     mapped to a local-state failure.
    /// </summary>
    [Fact]
    public async Task CanceledTokenPropagatesOperationCanceledException()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        using var canceledSource = new CancellationTokenSource();
        canceledSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Store.CreateOrReturnActiveAsync(fixture.Acceptance(), canceledSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Store.GetDetailAsync(runId, canceledSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Store.TakeNextPendingAsync(canceledSource.Token));
    }

    /// <summary>
    ///     Verifies that a pending run with a durable cancellation request is not claimed by
    ///     <see cref="ChangeLens.Core.AnalysisRuns.Interfaces.IAnalysisRunStore.TakeNextPendingAsync" />.
    /// </summary>
    [Fact]
    public async Task TakeNextPendingSkipsPendingRunWithDurableCancellationRequest()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        await fixture.SeedRepositoryAsync("/repository-cancelled");
        await fixture.SeedRepositoryAsync("/repository-active");
        var cancelledOutcome = await fixture.Store.CreateOrReturnActiveAsync(
            fixture.Acceptance("/repository-cancelled", requestedAtUnixMilliseconds: 1_000),
            TestContext.Current.CancellationToken);
        var activeOutcome = await fixture.Store.CreateOrReturnActiveAsync(
            fixture.Acceptance("/repository-active", requestedAtUnixMilliseconds: 2_000),
            TestContext.Current.CancellationToken);

        await fixture.Store.RequestCancellationAsync(cancelledOutcome.Data!.RunId!.Value, 1_500, TestContext.Current.CancellationToken);

        var firstTake = await fixture.Store.TakeNextPendingAsync(TestContext.Current.CancellationToken);
        var secondTake = await fixture.Store.TakeNextPendingAsync(TestContext.Current.CancellationToken);

        Assert.True(firstTake.IsSuccess);
        Assert.Equal(activeOutcome.Data!.RunId, firstTake.Data);
        Assert.True(secondTake.IsSuccess);
        Assert.Null(secondTake.Data);
        var cancelledRun = await fixture.GetRunAsync(cancelledOutcome.Data.RunId.Value);
        Assert.Equal(AnalysisRunState.PendingCapture, cancelledRun.State);
        Assert.Equal(1_500, cancelledRun.CancellationRequestedAtUnixMilliseconds);
    }

    /// <summary>
    ///     Verifies that concurrent <see cref="ChangeLens.Core.AnalysisRuns.Interfaces.IAnalysisRunStore.TakeNextPendingAsync" />
    ///     callers under WAL serialize cleanly without <c>SQLITE_BUSY</c> errors.
    /// </summary>
    [Fact]
    public async Task ConcurrentTakeNextPendingUnderWalSerializesCleanly()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var seededRunIds = new HashSet<Guid>();
        for (var index = 0; index < 8; index++)
        {
            var path = $"/repository-concurrent-{index}";
            await fixture.SeedRepositoryAsync(path);
            var accepted = await fixture.Store.CreateOrReturnActiveAsync(
                fixture.Acceptance(path, requestedAtUnixMilliseconds: 1_000 + index),
                TestContext.Current.CancellationToken);
            seededRunIds.Add(accepted.Data!.RunId!.Value);
        }

        using var barrier = new Barrier(8);
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () =>
            {
                var (context, store) = fixture.CreateIndependentStore();
                await using var _1 = context;
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                return await store.TakeNextPendingAsync(TestContext.Current.CancellationToken);
            }, TestContext.Current.CancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        var claimedIds = results.Select(result => result.Data!.Value).ToHashSet();
        Assert.Equal(seededRunIds, claimedIds);
    }

    /// <summary>
    ///     Verifies that high-frequency read polls log at <c>Debug</c> while mutating operations log at <c>Information</c>
    ///     with elapsed time and without sensitive paths.
    /// </summary>
    [Fact]
    public async Task ReadPollsLogAtDebugWhileMutatingOperationsLogAtInformationWithoutPaths()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var sensitivePath = "/Users/private/secret-repo-path";
        await fixture.SeedRepositoryAsync(sensitivePath);
        var logger = new RecordingInfrastructureLogger<ChangeLens.Infrastructure.AnalysisRuns.Services.SqliteAnalysisRunStore>();
        var (context, store) = fixture.CreateIndependentStore(logger);
        await using var _ = context;

        var accepted = await store.CreateOrReturnActiveAsync(
            fixture.Acceptance(sensitivePath),
            TestContext.Current.CancellationToken);
        Assert.True(accepted.IsSuccess);
        var runId = accepted.Data!.RunId!.Value;
        var entriesAfterAccept = logger.Entries.Count;

        var detail = await store.GetDetailAsync(runId, TestContext.Current.CancellationToken);
        var active = await store.GetActiveByRepositoryAsync(sensitivePath, TestContext.Current.CancellationToken);

        Assert.True(detail.IsSuccess);
        Assert.True(active.IsSuccess);
        Assert.Contains(
            logger.Entries.Take(entriesAfterAccept),
            entry => entry.Level == LogLevel.Information && entry.Message.Contains(" ms.", StringComparison.Ordinal));
        var readEntries = logger.Entries.Skip(entriesAfterAccept).ToArray();
        Assert.Equal(2, readEntries.Length);
        Assert.All(readEntries, entry => Assert.Equal(LogLevel.Debug, entry.Level));
        Assert.All(
            logger.Entries.Where(entry => entry.Level >= LogLevel.Information),
            entry => Assert.DoesNotContain(sensitivePath, entry.Message, StringComparison.Ordinal));
    }

    private static void AssertUnavailable(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(LocalStateErrorCode.Unavailable, Assert.Single(result.Errors).Code);
    }

    private static void AssertUnavailable<T>(Result<T> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(LocalStateErrorCode.Unavailable, Assert.Single(result.Errors).Code);
    }
}
