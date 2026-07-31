using System.Diagnostics;
using System.Text.Json;
using ChangeLens.Engine.IntegrationTests.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies that the real Engine process routes every approved protocol action to an implementation.
/// </summary>
/// <remarks>
///     The action names are written literally so this test is an independent catalog of the approved protocol action
///     set rather than a restatement of the production registrations it is meant to check.
/// </remarks>
public sealed class EngineActionRoutingTests
{
    /// <summary>
    ///     Verifies that an approved action is never reported as unknown and never fails unexpectedly.
    /// </summary>
    /// <param name="action">The approved protocol action name.</param>
    /// <param name="isPayloadFree">
    ///     <see langword="true" /> when the action carries no payload and therefore reaches its local-state-backed
    ///     capability even when the request omits parameters, as this test does.
    /// </param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Theory]
    [InlineData("repositories.open", false)]
    [InlineData("repositories.restoreLast", true)]
    [InlineData("repositories.listRecent", true)]
    [InlineData("repositories.removeRecent", false)]
    [InlineData("comparisons.listTargets", false)]
    [InlineData("comparisons.prepare", false)]
    [InlineData("comparisons.checkFreshness", false)]
    [InlineData("comparisons.checkRemoteBaseline", false)]
    [InlineData("comparisons.refreshRemoteBaseline", false)]
    [InlineData("engine.checkStatus", true)]
    [InlineData("preferences.getColorTheme", true)]
    [InlineData("preferences.setColorTheme", false)]
    [InlineData("analysis.start", false)]
    [InlineData("analysis.getActive", false)]
    [InlineData("analysis.pollRun", false)]
    [InlineData("analysis.cancel", false)]
    public async Task EngineRoutesEveryApprovedActionToAnImplementation(string action, bool isPayloadFree)
    {
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync(
            $"{{\"protocolVersion\":1,\"requestId\":\"routing\",\"action\":\"{action}\"}}");
        using var response = await ReadResponseAsync(engine);

        var root = response.RootElement;
        Assert.Equal("routing", root.GetProperty("requestId").GetString());

        var codes = root.GetProperty("type").GetString() == "error"
            ? root.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("code").GetString()!).ToArray()
            : Array.Empty<string>();

        Assert.DoesNotContain("protocol.unknownAction", codes);
        Assert.DoesNotContain("engine.unexpectedFailure", codes);

        if (isPayloadFree)
        {
            Assert.DoesNotContain("localState.unavailable", codes);
        }
    }

    private static async Task<JsonDocument> ReadResponseAsync(Process engine)
    {
        var responseLine = await engine.StandardOutput
            .ReadLineAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(responseLine));
        return JsonDocument.Parse(responseLine);
    }

    private static Process StartEngine()
    {
        var engineProject = Path.Combine(
            RepositoryPaths.Root,
            "src",
            "engine",
            "ChangeLens.Engine",
            "ChangeLens.Engine.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.Environment["ChangeLens__LocalState__Directory"] = Path.Combine(
            Path.GetTempPath(),
            "ChangeLens.Engine.IntegrationTests",
            Guid.NewGuid().ToString("N"));

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(engineProject);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The ChangeLens engine process could not be started.");
    }
}
