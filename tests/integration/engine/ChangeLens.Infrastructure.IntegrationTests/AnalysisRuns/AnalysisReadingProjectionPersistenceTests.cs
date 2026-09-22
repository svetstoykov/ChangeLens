using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Infrastructure.AnalysisRuns.Services;
using ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns.Support;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns;

/// <summary>
///     Verifies that the analysis run store records stage counts only while a run is active and persists the
///     renderable reading projection only on a successful first terminal commit.
/// </summary>
public sealed class AnalysisReadingProjectionPersistenceTests
{
    /// <summary>
    ///     Verifies that the recorded count columns start <see langword="null" /> on a new run's detail and that
    ///     each recorded value round-trips, including a stored zero.
    /// </summary>
    [Fact]
    public async Task CountColumnsStartNullAndRoundTripRecordedValues()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var initial = await fixture.Store.GetDetailAsync(runId, TestContext.Current.CancellationToken);

        Assert.True(initial.IsSuccess);
        Assert.Null(initial.Data!.CorrespondenceCandidateCount);
        Assert.Null(initial.Data.DisclosedEvidenceNodeCount);
        Assert.Null(initial.Data.ValidationRemovalCount);

        var correspondence = await fixture.Store.RecordCorrespondenceCandidateCountAsync(
            runId, 0, TestContext.Current.CancellationToken);
        var disclosed = await fixture.Store.RecordDisclosedEvidenceNodeCountAsync(
            runId, 7, TestContext.Current.CancellationToken);
        var removals = await fixture.Store.RecordValidationRemovalCountAsync(
            runId, 3, TestContext.Current.CancellationToken);

        Assert.True(correspondence.IsSuccess);
        Assert.True(disclosed.IsSuccess);
        Assert.True(removals.IsSuccess);
        var recorded = await fixture.Store.GetDetailAsync(runId, TestContext.Current.CancellationToken);
        Assert.Equal(0, recorded.Data!.CorrespondenceCandidateCount);
        Assert.Equal(7, recorded.Data.DisclosedEvidenceNodeCount);
        Assert.Equal(3, recorded.Data.ValidationRemovalCount);
    }

    /// <summary>
    ///     Verifies that recording a count for a run that already committed a terminal state leaves the count
    ///     <see langword="null" /> while still reporting success.
    /// </summary>
    [Fact]
    public async Task RecordingCountsOnTerminalRunLeavesThemNull()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Failed, 10_000, null, "analysis.unexpectedFailure");
        var commit = await fixture.Store.CommitTerminalAsync(runId, terminal, null, TestContext.Current.CancellationToken);
        Assert.True(commit.Data);

        var correspondence = await fixture.Store.RecordCorrespondenceCandidateCountAsync(
            runId, 5, TestContext.Current.CancellationToken);
        var disclosed = await fixture.Store.RecordDisclosedEvidenceNodeCountAsync(
            runId, 6, TestContext.Current.CancellationToken);
        var removals = await fixture.Store.RecordValidationRemovalCountAsync(
            runId, 8, TestContext.Current.CancellationToken);

        Assert.True(correspondence.IsSuccess);
        Assert.True(disclosed.IsSuccess);
        Assert.True(removals.IsSuccess);
        var detail = await fixture.Store.GetDetailAsync(runId, TestContext.Current.CancellationToken);
        Assert.Null(detail.Data!.CorrespondenceCandidateCount);
        Assert.Null(detail.Data.DisclosedEvidenceNodeCount);
        Assert.Null(detail.Data.ValidationRemovalCount);
    }

    /// <summary>
    ///     Verifies that a successful completed terminal commit with a projection stores both JSON documents
    ///     exactly and reads them back through the projection reader.
    /// </summary>
    [Fact]
    public async Task CompletedTerminalCommitStoresBothProjectionDocuments()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var projection = new AnalysisReadingProjection("{\"reading\":\"model\"}", "[{\"path\":\"a.cs\"}]");
        var terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Completed, 10_000, null, null);

        var commit = await fixture.Store.CommitTerminalAsync(runId, terminal, projection, TestContext.Current.CancellationToken);
        var stored = await fixture.Store.GetReadingProjectionAsync(runId, TestContext.Current.CancellationToken);

        Assert.True(commit.Data);
        Assert.True(stored.IsSuccess);
        Assert.NotNull(stored.Data);
        Assert.Equal(projection.ReadingModelJson, stored.Data!.ReadingModelJson);
        Assert.Equal(projection.ValidationRemovalsJson, stored.Data.ValidationRemovalsJson);
    }

    /// <summary>
    ///     Verifies that a second terminal commit for the same run returns <see langword="false" /> and does not
    ///     overwrite the projection stored by the first commit.
    /// </summary>
    [Fact]
    public async Task SecondTerminalCommitDoesNotOverwriteStoredProjection()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();
        var firstProjection = new AnalysisReadingProjection("{\"reading\":\"first\"}", "[{\"path\":\"a.cs\"}]");
        var secondProjection = new AnalysisReadingProjection("{\"reading\":\"second\"}", "[]");
        var firstTerminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Completed, 10_000, null, null);
        var secondTerminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Failed, 11_000, null, "analysis.unexpectedFailure");

        var first = await fixture.Store.CommitTerminalAsync(
            runId, firstTerminal, firstProjection, TestContext.Current.CancellationToken);
        var second = await fixture.Store.CommitTerminalAsync(
            runId, secondTerminal, secondProjection, TestContext.Current.CancellationToken);
        var stored = await fixture.Store.GetReadingProjectionAsync(runId, TestContext.Current.CancellationToken);

        Assert.True(first.Data);
        Assert.False(second.Data);
        Assert.NotNull(stored.Data);
        Assert.Equal(firstProjection.ReadingModelJson, stored.Data!.ReadingModelJson);
        Assert.Equal(firstProjection.ValidationRemovalsJson, stored.Data.ValidationRemovalsJson);
    }

    /// <summary>
    ///     Verifies that failed and cancelled terminal commits with a <see langword="null" /> projection leave both
    ///     JSON columns <see langword="null" />, keep the previously recorded counts, and read back null data.
    /// </summary>
    [Fact]
    public async Task FailedAndCancelledCommitsWithoutProjectionLeaveJsonNullAndKeepCounts()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var failedRunId = await fixture.CreateAcceptedRunAsync();
        await this.RecordCountsAsync(fixture, failedRunId, 4, 5, 6);
        var failedTerminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Failed, 10_000, null, "analysis.unexpectedFailure");

        var failedCommit = await fixture.Store.CommitTerminalAsync(
            failedRunId, failedTerminal, null, TestContext.Current.CancellationToken);

        var cancelledRunId = await fixture.CreateAcceptedRunAsync();
        await this.RecordCountsAsync(fixture, cancelledRunId, 7, 8, 9);
        var cancelledTerminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Cancelled, 12_000, null, null);
        var cancelledCommit = await fixture.Store.CommitTerminalAsync(
            cancelledRunId, cancelledTerminal, null, TestContext.Current.CancellationToken);

        Assert.True(failedCommit.Data);
        Assert.True(cancelledCommit.Data);
        var failedRun = await fixture.GetRunAsync(failedRunId);
        Assert.Null(failedRun.ReadingModelJson);
        Assert.Null(failedRun.ValidationRemovalsJson);
        var cancelledRun = await fixture.GetRunAsync(cancelledRunId);
        Assert.Null(cancelledRun.ReadingModelJson);
        Assert.Null(cancelledRun.ValidationRemovalsJson);

        var failedDetail = await fixture.Store.GetDetailAsync(failedRunId, TestContext.Current.CancellationToken);
        Assert.Equal(4, failedDetail.Data!.CorrespondenceCandidateCount);
        Assert.Equal(5, failedDetail.Data.DisclosedEvidenceNodeCount);
        Assert.Equal(6, failedDetail.Data.ValidationRemovalCount);
        var cancelledDetail = await fixture.Store.GetDetailAsync(cancelledRunId, TestContext.Current.CancellationToken);
        Assert.Equal(7, cancelledDetail.Data!.CorrespondenceCandidateCount);
        Assert.Equal(8, cancelledDetail.Data.DisclosedEvidenceNodeCount);
        Assert.Equal(9, cancelledDetail.Data.ValidationRemovalCount);

        Assert.Null((await fixture.Store.GetReadingProjectionAsync(failedRunId, TestContext.Current.CancellationToken)).Data);
        Assert.Null((await fixture.Store.GetReadingProjectionAsync(cancelledRunId, TestContext.Current.CancellationToken)).Data);
    }

    /// <summary>
    ///     Verifies that reading the projection of an active run succeeds with null data and that an unknown run
    ///     identifier fails with the stable unknown-run code.
    /// </summary>
    [Fact]
    public async Task GetReadingProjectionReturnsNullDataForActiveRunAndUnknownRunFails()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateAcceptedRunAsync();

        var active = await fixture.Store.GetReadingProjectionAsync(runId, TestContext.Current.CancellationToken);
        var unknown = await fixture.Store.GetReadingProjectionAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(active.IsSuccess);
        Assert.Null(active.Data);
        Assert.True(unknown.IsFailure);
        Assert.Equal(AnalysisErrorCode.UnknownRun, Assert.Single(unknown.Errors).Code);
    }

    /// <summary>
    ///     Verifies that detailed reads of a completed run with a stored projection succeed and report the counts
    ///     recorded while the run was still active.
    /// </summary>
    [Fact]
    public async Task GetDetailOnCompletedRunReportsCountsWithoutReadingProjection()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateCapturingRunAsync();
        await this.RecordCountsAsync(fixture, runId, 11, 12, 13);
        var projection = new AnalysisReadingProjection("{\"reading\":\"model\"}", "[{\"path\":\"a.cs\"}]");
        var terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Completed, 10_000, null, null);
        await fixture.Store.CommitTerminalAsync(runId, terminal, projection, TestContext.Current.CancellationToken);

        var detail = await fixture.Store.GetDetailAsync(runId, TestContext.Current.CancellationToken);

        Assert.True(detail.IsSuccess);
        Assert.Equal(AnalysisRunState.Completed, detail.Data!.State);
        Assert.Equal(11, detail.Data.CorrespondenceCandidateCount);
        Assert.Equal(12, detail.Data.DisclosedEvidenceNodeCount);
        Assert.Equal(13, detail.Data.ValidationRemovalCount);
    }

    /// <summary>
    ///     Verifies that a store over a fresh context on the same database still reads a projection committed by a
    ///     previous context, and that startup interruption changes only the active run.
    /// </summary>
    [Fact]
    public async Task RestartReadsProjectionAndInterruptionLeavesCompletedRunUntouched()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var completedRunId = await fixture.CreateAcceptedRunAsync();
        var projection = new AnalysisReadingProjection("{\"reading\":\"model\"}", "[{\"path\":\"a.cs\"}]");
        var terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Completed, 10_000, null, null);
        var commit = await fixture.Store.CommitTerminalAsync(
            completedRunId, terminal, projection, TestContext.Current.CancellationToken);
        Assert.True(commit.Data);
        var activeRunId = await fixture.CreateAcceptedRunAsync();

        var connectionString = fixture.Context.Database.GetConnectionString();
        await using var restartContext = new ChangeLensLocalStateDbContext(
            new DbContextOptionsBuilder<ChangeLensLocalStateDbContext>().UseSqlite(connectionString).Options);
        var restartedStore = new SqliteAnalysisRunStore(
            restartContext, TimeProvider.System, NullLogger<SqliteAnalysisRunStore>.Instance);

        var restartedProjection = await restartedStore.GetReadingProjectionAsync(
            completedRunId, TestContext.Current.CancellationToken);
        Assert.True(restartedProjection.IsSuccess);
        Assert.NotNull(restartedProjection.Data);
        Assert.Equal(projection.ReadingModelJson, restartedProjection.Data!.ReadingModelJson);
        Assert.Equal(projection.ValidationRemovalsJson, restartedProjection.Data.ValidationRemovalsJson);

        var interrupted = await restartedStore.InterruptActiveRunsAsync(20_000, TestContext.Current.CancellationToken);

        Assert.Equal(1, interrupted.Data);
        var activeRun = await fixture.GetRunAsync(activeRunId);
        Assert.Equal(AnalysisRunState.Interrupted, activeRun.State);
        var activeProjection = await restartedStore.GetReadingProjectionAsync(activeRunId, TestContext.Current.CancellationToken);
        Assert.True(activeProjection.IsSuccess);
        Assert.Null(activeProjection.Data);
        var completedRun = await fixture.GetRunAsync(completedRunId);
        Assert.Equal(AnalysisRunState.Completed, completedRun.State);
        Assert.Equal(projection.ReadingModelJson, completedRun.ReadingModelJson);
        Assert.Equal(projection.ValidationRemovalsJson, completedRun.ValidationRemovalsJson);
    }

    private async Task RecordCountsAsync(
        AnalysisRunStoreTestFixture fixture,
        Guid runId,
        int correspondence,
        int disclosed,
        int removals)
    {
        await fixture.Store.RecordCorrespondenceCandidateCountAsync(runId, correspondence, TestContext.Current.CancellationToken);
        await fixture.Store.RecordDisclosedEvidenceNodeCountAsync(runId, disclosed, TestContext.Current.CancellationToken);
        await fixture.Store.RecordValidationRemovalCountAsync(runId, removals, TestContext.Current.CancellationToken);
    }
}