using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Handlers;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.IntegrationTests.Analysis.Handlers.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Handlers;

/// <summary>Verifies the analysis-start protocol action mapping.</summary>
public sealed class AnalysisStartHandlerTests
{
    [Fact]
    public async Task AcceptedOutcomeMapsRunIdAndRequestedAt()
    {
        var runId = Guid.Parse("0198a1b2-3c4d-4e5f-8a9b-0123456789ab");
        var handler = new AnalysisStartHandler(
            new StubAnalysisRunCoordinator(
                start: (_, _, _, _, _, _) => Task.FromResult<Result<AnalysisStartOutcome>>(
                    new AnalysisStartOutcome(AnalysisStartOutcomeKind.Accepted, runId, 42, null))),
            new StubEngineProtocolSerializer(new AnalysisStartParameters
            {
                Path = "/repo",
                Target = "refs/heads/main",
                FreshnessToken = new string('0', 64),
                Checks = new AnalysisCheckSelectionParameters { Build = true, Tests = true },
            }));

        var response = await handler.HandleAsync(CreateRequest(JsonSerializer.SerializeToElement(new { })), TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<AnalysisStartResult>>(response).Result;
        var accepted = Assert.IsType<AcceptedAnalysisStartResult>(result);
        Assert.Equal(runId.ToString(), accepted.RunId);
        Assert.Equal(42, accepted.RequestedAt);
    }

    [Fact]
    public async Task RejectedOutcomesMapTheirTaggedStates()
    {
        var outcomes = new[]
        {
            new AnalysisStartOutcome(AnalysisStartOutcomeKind.RejectedStale, null, null, null),
            new AnalysisStartOutcome(
                AnalysisStartOutcomeKind.RejectedActive,
                null,
                null,
                Guid.Parse("1298a1b2-3c4d-4e5f-8a9b-0123456789ab")),
        };

        foreach (var outcome in outcomes)
        {
            var handler = new AnalysisStartHandler(
                new StubAnalysisRunCoordinator(start: (_, _, _, _, _, _) => Task.FromResult<Result<AnalysisStartOutcome>>(outcome)),
                new StubEngineProtocolSerializer(CreateParameters()));
            var response = await handler.HandleAsync(CreateRequest(JsonSerializer.SerializeToElement(new { })), TestContext.Current.CancellationToken);
            var result = Assert.IsType<ProtocolResultResponse<AnalysisStartResult>>(response).Result;

            if (outcome.Kind == AnalysisStartOutcomeKind.RejectedStale)
            {
                Assert.IsType<RejectedStaleAnalysisStartResult>(result);
            }
            else
            {
                var rejected = Assert.IsType<RejectedActiveAnalysisStartResult>(result);
                Assert.Equal(outcome.ActiveRunId.ToString(), rejected.ActiveRunId);
            }
        }
    }

    [Fact]
    public async Task CoordinatorFailurePreservesItsErrorCode()
    {
        var handler = new AnalysisStartHandler(
            new StubAnalysisRunCoordinator(start: (_, _, _, _, _, _) => Task.FromResult<Result<AnalysisStartOutcome>>(
                OperationError.NotFound("Repository unavailable.", "analysis.repositoryUnavailable"))),
            new StubEngineProtocolSerializer(CreateParameters()));

        var response = await handler.HandleAsync(CreateRequest(JsonSerializer.SerializeToElement(new { })), TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal("analysis.repositoryUnavailable", error.Code);
    }

    [Fact]
    public async Task MissingParametersReturnsValidationError()
    {
        var handler = new AnalysisStartHandler(
            new StubAnalysisRunCoordinator(),
            new EngineProtocolSerializer());

        var response = await handler.HandleAsync(CreateRequest(default), TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal("protocol.invalidRequest", error.Code);
    }

    private static AnalysisStartParameters CreateParameters() => new()
    {
        Path = "/repo",
        Target = "refs/heads/main",
        FreshnessToken = new string('0', 64),
        Checks = new AnalysisCheckSelectionParameters { Build = true, Tests = false },
    };

    private static EngineProtocolRequest CreateRequest(JsonElement parameters = default) => new()
    {
        ProtocolVersion = EngineProtocolConstants.CurrentVersion,
        RequestId = "analysis-start-test",
        Action = AnalysisActionConstants.StartAction,
        Parameters = parameters,
    };
}
