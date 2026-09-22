using System.Text.Json;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ClaimChecking.Constants;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.ClaimChecking.Services;
using ChangeLens.Infrastructure.IntegrationTests.ModelCompletion.Support;
using ChangeLens.Infrastructure.ModelCompletion.Models;
using ChangeLens.Infrastructure.ModelCompletion.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.ClaimChecking;

/// <summary>
///     Verifies the model-backed checker's request on the wire through the OpenAI-compatible adapter.
/// </summary>
public sealed class ModelClaimCheckerTransportTests
{
    /// <summary>
    ///     Asynchronously verifies one checker call sends the checker prompt and claims payload with no tool definitions.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CheckAsync_RequestCarriesNoToolDefinitions()
    {
        const string reply = """{"checks":[{"claimId":"track:t1:summary","verdict":"Supported","correctedKind":null,"focus":null}]}""";
        await using var server = await LoopbackHttpServer.StartAsync(static (_, _) => Task.FromResult(new LoopbackHttpResponse(
            200, $"{{\"model\":\"provider-model\",\"choices\":[{{\"message\":{{\"content\":{JsonSerializer.Serialize(reply)}}}}}]}}")));
        using var httpClient = new HttpClient();
        var options = new ModelCompletionOptions
        {
            BaseUrl = new Uri(server.BaseAddress, "v1").ToString(),
            Model = "fixture-model",
            ApiKey = "fixture-secret-key",
        };
        var client = new OpenAiCompatibleModelCompletionClient(
            httpClient, Options.Create(options), NullLogger<OpenAiCompatibleModelCompletionClient>.Instance);
        var checker = new ModelClaimChecker(client, new ClaimCheckingOptions(), NullLogger<ModelClaimChecker>.Instance);
        var claim = new CheckerClaim(
            "track:t1:summary",
            CheckerClaimType.Summary,
            "Summary",
            null,
            null,
            null,
            [new CheckerQuote("n1", "src/n1.cs", ChangeAnatomySide.After, 1, 1, null, "1| quote")]);

        var result = await checker.CheckAsync([claim], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ClaimVerdictKind.Supported, Assert.Single(result.Data!.Verdicts).Kind);
        var request = Assert.Single(server.Requests);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.False(root.TryGetProperty("tools", out _));
        Assert.False(root.TryGetProperty("tool_choice", out _));
        Assert.False(root.TryGetProperty("functions", out _));
        var messages = root.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(2, messages.Length);
        Assert.StartsWith("You are the checker in ChangeLens.", messages[0].GetProperty("content").GetString(), StringComparison.Ordinal);
        Assert.Equal("track:t1:summary", JsonDocument.Parse(messages[1].GetProperty("content").GetString()!).RootElement
            .GetProperty("claims")[0].GetProperty("claimId").GetString());
    }
}
