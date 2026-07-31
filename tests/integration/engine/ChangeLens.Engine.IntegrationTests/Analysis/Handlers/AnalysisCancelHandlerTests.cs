using System.Text.Json;
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

/// <summary>Verifies the analysis-cancel protocol action mapping.</summary>
public sealed class AnalysisCancelHandlerTests
{
    [Fact]
    public async Task SuccessfulCancellationReturnsPayloadFreeResult()
    {
        var handler = new AnalysisCancelHandler(
            new StubAnalysisRunCoordinator(cancel: (_, _) => Task.FromResult(Result.Success())),
            new StubEngineProtocolSerializer(new AnalysisCancelParameters
            {
                RunId = "0198a1b2-3c4d-4e5f-8a9b-0123456789ab",
            }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<JsonElement?>>(response);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task UnknownRunReturnsUnknownRunError()
    {
        var handler = new AnalysisCancelHandler(
            new StubAnalysisRunCoordinator(cancel: (_, _) => Task.FromResult(
                Result.ErrorFromResult(Result.Fail(
                    OperationError.NotFound("No analysis run matches the supplied identifier.", "analysis.unknownRun"))))),
            new StubEngineProtocolSerializer(new AnalysisCancelParameters { RunId = Guid.NewGuid().ToString() }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("analysis.unknownRun", Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors).Code);
    }

    [Fact]
    public async Task MalformedRunIdIsRejectedBeforeCoordinatorCall()
    {
        var coordinator = new StubAnalysisRunCoordinator();
        var handler = new AnalysisCancelHandler(
            coordinator,
            new StubEngineProtocolSerializer(new AnalysisCancelParameters { RunId = "not-a-guid" }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("analysis.unknownRun", Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors).Code);
    }

    private static EngineProtocolRequest CreateRequest() => new()
    {
        ProtocolVersion = EngineProtocolConstants.CurrentVersion,
        RequestId = "analysis-cancel-test",
        Action = AnalysisActionConstants.CancelAction,
        Parameters = JsonSerializer.SerializeToElement(new { runId = "ignored" }),
    };
}
