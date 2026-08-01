using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Handlers;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.AnalysisRuns.Services;
using ChangeLens.Engine.IntegrationTests.Analysis.Handlers.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Handlers;

/// <summary>Verifies the analysis-getActive protocol action mapping.</summary>
public sealed class AnalysisGetActiveHandlerTests
{
    [Fact]
    public async Task NoActiveRunMapsToNone()
    {
        var handler = new AnalysisGetActiveHandler(
            new StubAnalysisRunCoordinator(getActive: (_, _) => Task.FromResult(Result.Success<AnalysisRunDetail?>(null))),
            new StubEngineProtocolSerializer(new AnalysisGetActiveParameters { Path = "/repo" }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<NoneAnalysisGetActiveResult>(Assert.IsType<ProtocolResultResponse<AnalysisGetActiveResult>>(response).Result);
    }

    [Fact]
    public async Task ActiveRunMapsEveryProjectionField()
    {
        var detail = CreateDetail();
        var handler = new AnalysisGetActiveHandler(
            new StubAnalysisRunCoordinator(getActive: (_, _) => Task.FromResult<Result<AnalysisRunDetail?>>(detail)),
            new StubEngineProtocolSerializer(new AnalysisGetActiveParameters { Path = "/repo" }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var actual = Assert.IsType<ActiveAnalysisGetActiveResult>(
            Assert.IsType<ProtocolResultResponse<AnalysisGetActiveResult>>(response).Result).Run;
        var expected = AnalysisProjectionMapper.ToProtocol(detail).Data!;
        Assert.Equal(expected.RunId, actual.RunId);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.Repository, actual.Repository);
        Assert.Equal(expected.Comparison, actual.Comparison);
        Assert.Equal(expected.RequestedAt, actual.RequestedAt);
        Assert.Equal(expected.CaptureStartedAt, actual.CaptureStartedAt);
        Assert.Equal(expected.CancellationRequested, actual.CancellationRequested);
        Assert.Equal(expected.Terminal, actual.Terminal);
        Assert.Equal(expected.InterruptedAt, actual.InterruptedAt);
        Assert.Equal(expected.InterruptionReason, actual.InterruptionReason);
    }

    [Fact]
    public async Task RepositoryFailurePreservesItsErrorCode()
    {
        var handler = new AnalysisGetActiveHandler(
            new StubAnalysisRunCoordinator(getActive: (_, _) => Task.FromResult<Result<AnalysisRunDetail?>>(
                OperationError.NotFound("Repository unavailable.", "analysis.repositoryUnavailable"))),
            new StubEngineProtocolSerializer(new AnalysisGetActiveParameters { Path = "/repo" }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("analysis.repositoryUnavailable", Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors).Code);
    }

    private static AnalysisRunDetail CreateDetail() => new(
        Guid.Parse("0198a1b2-3c4d-4e5f-8a9b-0123456789ab"),
        AnalysisRunState.CompletedWithLimitations,
        new AnalysisRepositoryIdentity(
            Guid.Parse("5298a1b2-3c4d-4e5f-8a9b-0123456789ab"),
            "change_lens",
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
        RequestId = "analysis-active-test",
        Action = AnalysisActionConstants.GetActiveAction,
        Parameters = JsonSerializer.SerializeToElement(new { path = "/repo" }),
    };
}
