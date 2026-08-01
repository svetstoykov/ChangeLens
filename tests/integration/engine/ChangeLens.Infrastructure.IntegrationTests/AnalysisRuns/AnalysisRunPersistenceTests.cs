using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Infrastructure.AnalysisRuns.Persistence.Entities;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.LocalState.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns;

/// <summary>
///     Verifies that every <see cref="AnalysisRunState" />, <see cref="AnalysisRunStepState" />, and
///     <see cref="AnalysisStage" /> value round-trips through SQLite without violating a check constraint.
/// </summary>
public sealed class AnalysisRunPersistenceTests
{
    private static readonly AnalysisRunState[] TerminalAtStates =
    [
        AnalysisRunState.Completed,
        AnalysisRunState.CompletedWithLimitations,
        AnalysisRunState.Cancelled,
        AnalysisRunState.Failed,
    ];

    /// <summary>
    ///     Verifies that saving one analysis run per <see cref="AnalysisRunState" /> value succeeds against the
    ///     <c>CK_analysis_runs_state</c>, terminal, and interruption check constraints.
    /// </summary>
    [Fact]
    public async Task EveryAnalysisRunStateSavesAndRoundTrips()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = LocalStatePaths.Resolve(temporaryDirectory.DirectoryPath);
        await using var context = CreateContext(paths);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.True((await CreateInitializer(context, paths).InitializeAsync(cancellationToken)).IsSuccess);

        var states = Enum.GetValues<AnalysisRunState>();
        foreach (var state in states)
        {
            context.AnalysisRuns.Add(CreateRun(state));
        }

        await context.SaveChangesAsync(cancellationToken);

        var savedStates = await context.AnalysisRuns.Select(run => run.State).ToListAsync(cancellationToken);
        Assert.Equal(states.OrderBy(state => state), savedStates.OrderBy(state => state));
    }

    /// <summary>
    ///     Verifies that saving one analysis run step per <see cref="AnalysisRunStepState" /> value, cycling
    ///     through every <see cref="AnalysisStage" /> value, succeeds against the <c>CK_analysis_run_steps_state</c>
    ///     check constraint.
    /// </summary>
    [Fact]
    public async Task EveryAnalysisRunStepStateAndStageSavesAndRoundTrips()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = LocalStatePaths.Resolve(temporaryDirectory.DirectoryPath);
        await using var context = CreateContext(paths);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.True((await CreateInitializer(context, paths).InitializeAsync(cancellationToken)).IsSuccess);

        var run = CreateRun(AnalysisRunState.Persisting);
        var stepStates = Enum.GetValues<AnalysisRunStepState>();
        var stages = Enum.GetValues<AnalysisStage>();
        for (var index = 0; index < stepStates.Length; index++)
        {
            run.Steps.Add(new AnalysisRunStepEntity
            {
                RunId = run.RunId,
                StepId = $"step-{index}",
                Producer = "test-producer",
                Capability = "test-capability",
                Order = index,
                Stage = stages[index % stages.Length],
                State = stepStates[index],
                Run = run,
            });
        }

        context.AnalysisRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken);

        var savedSteps = await context.AnalysisRunSteps
            .Where(step => step.RunId == run.RunId)
            .ToListAsync(cancellationToken);
        Assert.Equal(stepStates.OrderBy(state => state), savedSteps.Select(step => step.State).OrderBy(state => state));
        Assert.Equal(stages, savedSteps.OrderBy(step => step.Order).Select(step => step.Stage).Distinct().OrderBy(stage => stage));
    }

    private static AnalysisRunEntity CreateRun(AnalysisRunState state)
    {
        var runId = Guid.NewGuid();
        var isTerminal = TerminalAtStates.Contains(state);
        var isInterrupted = state == AnalysisRunState.Interrupted;
        return new AnalysisRunEntity
        {
            RunId = runId,
            RepositoryId = Guid.NewGuid(),
            RepositoryDisplayName = "change_lens",
            CanonicalRepositoryPath = $"/projects/{runId:N}",
            CanonicalRepositoryPathKey = $"/projects/{runId:N}",
            HeadRevision = "abc123",
            Target = "main",
            TargetRevision = "def456",
            FreshnessToken = "freshness-token",
            State = state,
            RequestedAtUnixMilliseconds = 100,
            TerminalAtUnixMilliseconds = isTerminal ? 200 : null,
            InterruptedAtUnixMilliseconds = isInterrupted ? 200 : null,
        };
    }

    private static ChangeLensLocalStateDbContext CreateContext(LocalStatePaths paths)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = 5,
            ForeignKeys = true,
        }.ToString();
        var options = new DbContextOptionsBuilder<ChangeLensLocalStateDbContext>().UseSqlite(connectionString).Options;
        return new ChangeLensLocalStateDbContext(options);
    }

    private static SqliteLocalStateInitializer CreateInitializer(
        ChangeLensLocalStateDbContext context,
        LocalStatePaths paths) =>
        new(context, paths, NullLogger<SqliteLocalStateInitializer>.Instance);
}
