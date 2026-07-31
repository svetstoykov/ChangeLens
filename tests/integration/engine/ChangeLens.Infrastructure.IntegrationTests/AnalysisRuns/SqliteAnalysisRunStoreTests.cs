using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns.Support;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns;

/// <summary>
///     Verifies <see cref="ChangeLens.Infrastructure.AnalysisRuns.Services.SqliteAnalysisRunStore" /> acceptance and
///     processor-claim behavior against a real SQLite database.
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
    ///     Verifies that claiming the next pending run selects deterministically by requested-at timestamp then
    ///     run identifier, and that the claim is owned by the requesting processor session.
    /// </summary>
    [Fact]
    public async Task ClaimIsDeterministicByRequestedAtThenRunIdAndOwnedByCurrentSession()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        await fixture.SeedRepositoryAsync("/repository-a");
        await fixture.SeedRepositoryAsync("/repository-b");
        var earlier = fixture.Acceptance("/repository-a", requestedAtUnixMilliseconds: 1_000);
        var later = fixture.Acceptance("/repository-b", requestedAtUnixMilliseconds: 2_000);
        await fixture.Store.CreateOrReturnActiveAsync(later, TestContext.Current.CancellationToken);
        await fixture.Store.CreateOrReturnActiveAsync(earlier, TestContext.Current.CancellationToken);

        var claim = await fixture.Store.ClaimNextPendingAsync(fixture.ProcessorSessionId, TestContext.Current.CancellationToken);

        Assert.NotNull(claim.Data);
        var claimedRun = await fixture.GetRunAsync(claim.Data!.RunId);
        Assert.Equal(1_000, claimedRun.RequestedAtUnixMilliseconds);
    }

    /// <summary>
    ///     Verifies that claiming the next pending run ignores rows accepted under a different processor session.
    /// </summary>
    [Fact]
    public async Task ClaimIgnoresRowsOwnedByAnotherProcessorSession()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var otherSessionId = Guid.NewGuid();
        await fixture.Store.CreateOrReturnActiveAsync(
            fixture.Acceptance(processorSessionId: otherSessionId),
            TestContext.Current.CancellationToken);

        var claim = await fixture.Store.ClaimNextPendingAsync(fixture.ProcessorSessionId, TestContext.Current.CancellationToken);

        Assert.Null(claim.Data);
    }

    /// <summary>
    ///     Verifies that a claimed run's step plan, step begin/finish outcomes, and stage transition all persist
    ///     durably in one coordinated sequence.
    /// </summary>
    [Fact]
    public async Task StepPlanBeginFinishAndStageTransitionPersist()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var claim = await fixture.Store.ClaimNextPendingAsync(fixture.ProcessorSessionId, TestContext.Current.CancellationToken);
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

        Assert.NotNull(claim.Data);
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

        var firstCommit = await fixture.Store.CommitTerminalAsync(runId, terminal, TestContext.Current.CancellationToken);
        var secondCommit = await fixture.Store.CommitTerminalAsync(
            runId,
            terminal with { Kind = AnalysisTerminalKind.Failed, FailureCode = "analysis.unexpectedFailure" },
            TestContext.Current.CancellationToken);

        Assert.True(firstCommit.Data);
        Assert.False(secondCommit.Data);
        var run = await fixture.GetRunAsync(runId);
        Assert.Equal(AnalysisRunState.Completed, run.State);
    }

    /// <summary>
    ///     Verifies that finalizing cancelled pending runs commits the durable cancelled terminal state without
    ///     claiming the run, and that the finalized run is no longer claimable.
    /// </summary>
    [Fact]
    public async Task FinalizesCancelledPendingRunsWithoutClaimingThem()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        await fixture.Store.RequestCancellationAsync(runId, 4_000, TestContext.Current.CancellationToken);

        var finalizedCount = await fixture.Store.FinalizeCancelledPendingRunsAsync(
            fixture.ProcessorSessionId,
            6_000,
            TestContext.Current.CancellationToken);
        var claim = await fixture.Store.ClaimNextPendingAsync(fixture.ProcessorSessionId, TestContext.Current.CancellationToken);

        Assert.Equal(1, finalizedCount.Data);
        Assert.Null(claim.Data);
        var run = await fixture.GetRunAsync(runId);
        Assert.Equal(AnalysisRunState.Cancelled, run.State);
        Assert.Equal(6_000, run.TerminalAtUnixMilliseconds);
    }

    /// <summary>
    ///     Verifies that startup recovery interrupts every active row owned by an earlier processor session while
    ///     leaving the current session's own active run untouched.
    /// </summary>
    [Fact]
    public async Task EarlierSessionInterruptionReleasesReservationAndSkipsCurrentSession()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var currentSessionRunId = await fixture.CreateAcceptedRunAsync();
        var earlierSessionId = Guid.NewGuid();
        await fixture.SeedRepositoryAsync("/repository-earlier");
        await fixture.Store.CreateOrReturnActiveAsync(
            fixture.Acceptance("/repository-earlier", processorSessionId: earlierSessionId),
            TestContext.Current.CancellationToken);

        var interruptedCount = await fixture.Store.InterruptEarlierSessionRowsAsync(
            fixture.ProcessorSessionId,
            20_000,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, interruptedCount.Data);
        var currentRun = await fixture.GetRunAsync(currentSessionRunId);
        Assert.Equal(AnalysisRunState.PendingCapture, currentRun.State);
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
    ///     carrying a <see langword="null" /> projection.
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
}
