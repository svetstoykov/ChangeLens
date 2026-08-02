using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Handlers;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.IntegrationTests.Analysis.Handlers.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Handlers;

/// <summary>Verifies the analysis-pollRun protocol action mapping.</summary>
public sealed class AnalysisPollRunHandlerTests
{
    [Fact]
    public async Task MalformedRunIdReturnsUnknownRunWithoutCallingCoordinator()
    {
        var coordinator = new StubAnalysisRunCoordinator();
        var handler = new AnalysisPollRunHandler(
            coordinator,
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters { RunId = "not-a-guid" }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("analysis.unknownRun", Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors).Code);
        Assert.False(coordinator.PollCalled);
    }

    [Fact]
    public async Task UnknownWellFormedRunReturnsUnknownRun()
    {
        var runId = Guid.NewGuid();
        var handler = new AnalysisPollRunHandler(
            new StubAnalysisRunCoordinator(pollRun: (_, _) => Task.FromResult<Result<AnalysisRunDetail>>(
                OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun))),
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters { RunId = runId.ToString() }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("analysis.unknownRun", Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors).Code);
    }

    [Fact]
    public async Task PopulatedSummaryMapsCompletedWithLimitationsTerminal()
    {
        var detail = CreateDetail();
        var handler = new AnalysisPollRunHandler(
            new StubAnalysisRunCoordinator(pollRun: (_, _) => Task.FromResult<Result<AnalysisRunDetail>>(detail)),
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters
            {
                RunId = detail.RunId.ToString(),
            }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var summary = Assert.IsType<AnalysisRunSummaryResult>(Assert.IsType<ProtocolResultResponse<AnalysisRunSummaryResult>>(response).Result);
        var terminal = Assert.IsType<CompletedWithLimitationsAnalysisTerminalResult>(summary.Terminal);
        Assert.Equal(2, terminal.LimitationCount);
        Assert.Equal("completedWithLimitations", summary.State);
        Assert.Equal(detail.Repository.CanonicalPath, summary.Repository.CanonicalPath);
        Assert.Equal(detail.Comparison.TargetRevision, summary.Comparison.TargetRevision);
    }

    /// <summary>
    ///     Asynchronously projects snapshot identity and both capture facts for a captured run.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task HandleAsyncCapturedRunProjectsSnapshotIdentityAndFacts()
    {
        var snapshotId = Guid.NewGuid();
        var detail = CapturedDetail(snapshotId, capturedChangedFileCount: 34,
            counts: new ExcludedUncommittedCounts(3, 2, 0, 1, 0));

        var result = await PollAsync(detail);

        Assert.Equal(snapshotId.ToString(), result.SnapshotId);
        Assert.Equal(2_000, result.CapturedAt);
        Assert.Collection(result.Facts,
            fact =>
            {
                Assert.Equal(AnalysisFactKind.ChangedFilesCaptured, fact.Kind);
                Assert.Equal(34, fact.Count);
                Assert.Null(fact.Detail);
            },
            fact =>
            {
                Assert.Equal(AnalysisFactKind.ExcludedUncommittedFiles, fact.Kind);
                Assert.Equal(3, fact.Count);
                Assert.Equal("2 staged, 1 untracked", fact.Detail);
            });
    }

    /// <summary>
    ///     Asynchronously omits the exclusion fact when nothing uncommitted was excluded.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task HandleAsyncCapturedRunWithoutExclusionsEmitsOneFact()
    {
        var detail = CapturedDetail(Guid.NewGuid(), capturedChangedFileCount: 5,
            counts: new ExcludedUncommittedCounts(0, 0, 0, 0, 0));

        var result = await PollAsync(detail);

        Assert.Single(result.Facts);
        Assert.Equal(AnalysisFactKind.ChangedFilesCaptured, result.Facts[0].Kind);
    }

    private static async Task<AnalysisRunSummaryResult> PollAsync(AnalysisRunDetail detail)
    {
        var handler = new AnalysisPollRunHandler(
            new StubAnalysisRunCoordinator(pollRun: (_, _) => Task.FromResult<Result<AnalysisRunDetail>>(detail)),
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters { RunId = detail.RunId.ToString() }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        return Assert.IsType<AnalysisRunSummaryResult>(Assert.IsType<ProtocolResultResponse<AnalysisRunSummaryResult>>(response).Result);
    }

    private static AnalysisRunDetail CapturedDetail(Guid snapshotId, int capturedChangedFileCount, ExcludedUncommittedCounts counts) =>
        CreateDetail() with
        {
            CapturedAtUnixMilliseconds = 2_000,
            SnapshotId = snapshotId,
            ManifestHash = new string('a', 64),
            CapturedChangedFileCount = capturedChangedFileCount,
            ExcludedUncommittedCounts = counts,
        };

    private static AnalysisRunDetail CreateDetail() => new(
        Guid.Parse("0198a1b2-3c4d-4e5f-8a9b-0123456789ab"),
        AnalysisRunState.CompletedWithLimitations,
        new AnalysisRepositoryIdentity(
            Guid.Parse("5298a1b2-3c4d-4e5f-8a9b-0123456789ab"),
            "change_lens",
            "/projects/change_lens",
            "/projects/change_lens",
            "0123456789abcdef0123456789abcdef01234567"),
        new AnalysisComparisonIdentity(
            "refs/heads/feature/comparison",
            "89abcdef0123456789abcdef0123456789abcdef",
            new string('0', 64)),
        1720000000000,
        1720000000100,
        null,
        null,
        null,
        null,
        null,
        false,
        new AnalysisTerminalSummary(AnalysisTerminalKind.CompletedWithLimitations, 1720000000500, 2, null),
        null,
        null);

    private static EngineProtocolRequest CreateRequest() => new()
    {
        ProtocolVersion = EngineProtocolConstants.CurrentVersion,
        RequestId = "analysis-poll-test",
        Action = AnalysisActionConstants.PollRunAction,
        Parameters = JsonSerializer.SerializeToElement(new { runId = "ignored" }),
    };
}
