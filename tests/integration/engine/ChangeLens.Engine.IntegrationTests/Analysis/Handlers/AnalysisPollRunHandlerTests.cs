using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
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
    public async Task PopulatedProjectionMapsCompletedWithLimitationsTerminal()
    {
        var detail = CreateDetail();
        var handler = new AnalysisPollRunHandler(
            new StubAnalysisRunCoordinator(pollRun: (_, _) => Task.FromResult<Result<AnalysisRunDetail>>(detail)),
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters
            {
                RunId = detail.RunId.ToString(),
            }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var projection = Assert.IsType<AnalysisRunProjectionResult>(Assert.IsType<ProtocolResultResponse<AnalysisRunProjectionResult>>(response).Result);
        var terminal = Assert.IsType<CompletedWithLimitationsAnalysisTerminalResult>(projection.Terminal);
        Assert.Equal(2, terminal.LimitationCount);
        Assert.Equal("completedWithLimitations", projection.State);
        Assert.Equal(detail.Repository.CanonicalPath, projection.Repository.CanonicalPath);
        Assert.Equal(detail.Comparison.TargetRevision, projection.Comparison.TargetRevision);
    }

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
