using System.Text.Json;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Handlers;
using ChangeLens.Engine.IntegrationTests.Comparisons.Handlers.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Comparisons.Handlers;

/// <summary>
///     Verifies defensive comparison output mapping at the Engine protocol boundary.
/// </summary>
public sealed class ComparisonActionHandlerTests
{
    private const string RequestId = "comparison-defensive-mapping";

    /// <summary>
    ///     Asynchronously verifies an unapproved freshness state returns a stable internal error logged at warning.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task UnapprovedFreshnessStateReturnsWarningInternalError()
    {
        var handler = new ComparisonCheckFreshnessHandler(
            new StubRepositoryBusyGuard(),
            new StubGitComparisonFreshnessChecker(
                new ComparisonFreshnessCheck((ComparisonFreshnessState)int.MaxValue, null, null)),
            new EngineProtocolSerializer());
        var services = new ServiceCollection();
        services.AddKeyedScoped(
            typeof(IActionHandler),
            ComparisonCheckFreshnessHandler.Action,
            (_, _) => handler);
        using var serviceProvider = services.BuildServiceProvider();
        using var requestScope = serviceProvider.CreateScope();
        var logger = new RecordingLogger<EngineProtocolHost>();
        var host = new EngineProtocolHost(null!, null!, logger, null!);
        var request = CreateRequest(
            ComparisonActionConstants.CheckFreshnessAction,
            """{"path":"/repository","target":"refs/heads/main","freshnessToken":"token"}""");

        var response = await host.ProcessAsync(
            request,
            requestScope.ServiceProvider,
            TestContext.Current.CancellationToken);

        AssertInternalError(
            response,
            "comparison.unmappedFreshnessState",
            "The comparison freshness state is not approved for the engine protocol.");
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Levels));
    }

    /// <summary>
    ///     Asynchronously verifies an unapproved remote-baseline state returns its stable internal error.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task UnapprovedRemoteBaselineStateReturnsInternalError()
    {
        var handler = new ComparisonCheckRemoteBaselineHandler(
            new StubRepositoryBusyGuard(),
            new StubGitRemoteBaselineTracker(new RemoteBaselineCheckResult((RemoteBaselineState)int.MaxValue, null)),
            new EngineProtocolSerializer());
        var request = CreateRequest(
            ComparisonActionConstants.CheckRemoteBaselineAction,
            """{"path":"/repository","target":"refs/remotes/origin/main"}""");

        var response = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

        AssertInternalError(
            response,
            "comparison.unmappedRemoteBaselineState",
            "The remote baseline check result is not approved for the engine protocol.");
    }

    private static EngineProtocolRequest CreateRequest(string action, string parametersJson)
    {
        using var parameters = JsonDocument.Parse(parametersJson);
        return new EngineProtocolRequest
        {
            ProtocolVersion = EngineProtocolConstants.CurrentVersion,
            RequestId = RequestId,
            Action = action,
            Parameters = parameters.RootElement.Clone(),
        };
    }

    private static void AssertInternalError(ProtocolResponse response, string expectedCode, string expectedMessage)
    {
        var errorResponse = Assert.IsType<ProtocolErrorResponse>(response);
        Assert.Equal(RequestId, errorResponse.RequestId);
        var error = Assert.Single(errorResponse.Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedMessage, error.Message);
    }
}
