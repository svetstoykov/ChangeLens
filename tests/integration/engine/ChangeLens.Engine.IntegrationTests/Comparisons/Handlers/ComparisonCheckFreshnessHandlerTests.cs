using System.Text.Json;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Handlers;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.IntegrationTests.Comparisons.Handlers.Support;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Comparisons.Handlers;

/// <summary>
///     Verifies the richer freshness check maps to the approved engine protocol result shape.
/// </summary>
public sealed class ComparisonCheckFreshnessHandlerTests
{
    /// <summary>
    ///     Asynchronously verifies a current freshness check returns the current comparison freshness result.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CurrentStateReturnsCurrentResult()
    {
        var repository = new RepositoryDescriptor(
            "change_lens",
            "/repository",
            new DetachedRepositoryHead("0123456789abcdef0123456789abcdef01234567"));
        var handler = new ComparisonCheckFreshnessHandler(
            new StubGitComparisonFreshnessChecker(
                new ComparisonFreshnessCheck(
                    ComparisonFreshnessState.Current,
                    repository,
                    "0123456789abcdef0123456789abcdef01234567")),
            new EngineProtocolSerializer());
        var request = CreateRequest("""{"path":"/repository","target":"refs/heads/main","freshnessToken":"token"}""");

        var response = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<ComparisonFreshnessResult>>(response);
        Assert.IsType<CurrentComparisonFreshnessResult>(result.Result);
    }

    private static EngineProtocolRequest CreateRequest(string parametersJson)
    {
        using var parameters = JsonDocument.Parse(parametersJson);
        return new EngineProtocolRequest
        {
            ProtocolVersion = EngineProtocolConstants.CurrentVersion,
            RequestId = "comparison-freshness-handler",
            Action = ComparisonActionConstants.CheckFreshnessAction,
            Parameters = parameters.RootElement.Clone(),
        };
    }
}
