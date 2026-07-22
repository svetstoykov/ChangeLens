using System.Diagnostics;
using System.Text.Json;
using ChangeLens.Engine.IntegrationTests.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies engine-status behavior through the real process protocol boundary.
/// </summary>
public sealed class EngineStatusProtocolTests
{
    /// <summary>
    ///     Verifies that the real Engine result matches the shared cross-language fixture.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineStatusResultMatchesSharedFixture()
    {
        using var request = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                FixturePath("engine-check-status.request.json"),
                TestContext.Current.CancellationToken));
        using var expected = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                FixturePath("engine-check-status.result.json"),
                TestContext.Current.CancellationToken));
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request.RootElement));
        using var actual = await ReadResponseAsync(engine);

        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual.RootElement));
    }

    /// <summary>
    ///     Verifies that the engine exits successfully when protocol input closes.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineExitsSuccessfullyWhenStandardInputCloses()
    {
        using var engine = StartEngine();

        engine.StandardInput.Close();

        await WaitForExitAsync(engine);
        Assert.Equal(0, engine.ExitCode);
    }

    /// <summary>
    ///     Verifies that logs contain safe metadata without persisting raw protocol payloads.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineLogsMetadataWithoutCorruptingStandardOutputOrPersistingRawPayloads()
    {
        const string request =
            "{\"protocolVersion\":2,\"requestId\":\"logging-request\",\"action\":\"engine.checkStatus\"}";
        var logDirectory = Path.Combine(
            Path.GetTempPath(),
            "ChangeLens.Engine.IntegrationTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            using var engine = StartEngine(logDirectory, redirectStandardError: true);

            await engine.StandardInput.WriteLineAsync(request);
            engine.StandardInput.Close();

            var responseLine = await ReadResponseLineAsync(engine);
            await WaitForExitAsync(engine);
            var remainingOutput = await engine.StandardOutput.ReadToEndAsync(
                TestContext.Current.CancellationToken);
            var standardError = await engine.StandardError.ReadToEndAsync(
                TestContext.Current.CancellationToken);
            var logFile = Assert.Single(Directory.GetFiles(logDirectory, "changelens-engine-*.log"));
            var fileLog = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);

            Assert.Equal(string.Empty, remainingOutput);
            AssertLogContainsSafeOutcome(standardError, request, responseLine);
            AssertLogContainsSafeOutcome(fileLog, request, responseLine);
        }
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                Directory.Delete(logDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Verifies that input rejected before its common envelope is accepted matches the shared uncorrelated-error fixture.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task RejectedEnvelopeMatchesSharedUncorrelatedErrorFixture()
    {
        using var expected = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                FixturePath("uncorrelated-error.response.json"),
                TestContext.Current.CancellationToken));
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync(
            """{"protocolVersion":"1","requestId":"desktop-43","action":"engine.checkStatus"}""");
        using var actual = await ReadResponseAsync(engine);

        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual.RootElement));
    }

    /// <summary>
    ///     Verifies exact structured errors for known protocol failures.
    /// </summary>
    /// <param name="request">The request that produces the known failure.</param>
    /// <param name="expectedType">The expected broad error category.</param>
    /// <param name="expectedCode">The expected stable error code.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Theory]
    [InlineData("not-json", "MalformedInput", "protocol.invalidJson")]
    [InlineData("null", "Validation", "protocol.invalidRequest")]
    [InlineData("[]", "Validation", "protocol.invalidRequest")]
    [InlineData("{}", "Validation", "protocol.invalidRequest")]
    [InlineData(
        "{\"requestId\":\"id\",\"action\":\"engine.checkStatus\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"action\":\"engine.checkStatus\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"id\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"engine.checkStatus\"," +
        "\"requestId\":\"other\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"protocolVersion\":1,\"requestId\":\"id\"," +
        "\"action\":\"engine.checkStatus\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"engine.checkStatus\"," +
        "\"action\":\"engine.checkStatus\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"\",\"action\":\"engine.checkStatus\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":\"1\",\"requestId\":\"id\",\"action\":\"engine.checkStatus\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":1,\"action\":\"engine.checkStatus\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":1}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"engine.checkStatus\"," +
        "\"extra\":true}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":2,\"requestId\":\"id\",\"action\":\"engine.checkStatus\"}",
        "UnprocessableInput",
        "protocol.unsupportedVersion")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"unknown\"}",
        "NotFound",
        "protocol.unknownAction")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"engine.checkStatus\"," +
        "\"parameters\":null}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"engine.checkStatus\"," +
        "\"parameters\":{}}",
        "Validation",
        "protocol.invalidRequest")]
    public async Task EngineReturnsStructuredErrorForKnownProtocolFailure(
        string request,
        string expectedType,
        string expectedCode)
    {
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync(request);
        using var response = await ReadResponseAsync(engine);

        var root = response.RootElement;
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("error", root.GetProperty("type").GetString());
        var error = Assert.Single(root.GetProperty("errors").EnumerateArray());
        Assert.Equal(expectedType, error.GetProperty("type").GetString());
        Assert.Equal(expectedCode, error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
    }

    /// <summary>
    ///     Verifies that malformed input is rejected before a following valid action completes.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineProcessesValidActionAfterRejectedInput()
    {
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync("not-json");
        await engine.StandardInput.WriteLineAsync(
            """{"protocolVersion":1,"requestId":"request-valid","action":"engine.checkStatus"}""");
        engine.StandardInput.Close();

        using var first = await ReadResponseAsync(engine);
        using var second = await ReadResponseAsync(engine);
        await WaitForExitAsync(engine);

        Assert.Equal("protocol.invalidJson", FirstErrorCode(first));
        Assert.Equal("request-valid", second.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(JsonValueKind.Null, second.RootElement.GetProperty("result").ValueKind);
        Assert.Equal(0, engine.ExitCode);
    }

    /// <summary>
    ///     Verifies that one process handles multiple status actions sequentially.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineProcessesMultipleSequentialActions()
    {
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync(
            """{"protocolVersion":1,"requestId":"request-1","action":"engine.checkStatus"}""");
        await engine.StandardInput.WriteLineAsync(
            """{"protocolVersion":1,"requestId":"request-2","action":"engine.checkStatus"}""");
        engine.StandardInput.Close();

        using var first = await ReadResponseAsync(engine);
        using var second = await ReadResponseAsync(engine);
        await WaitForExitAsync(engine);

        Assert.Equal("request-1", first.RootElement.GetProperty("requestId").GetString());
        Assert.Equal("request-2", second.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(0, engine.ExitCode);
        Assert.Equal(
            string.Empty,
            await engine.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    private static void AssertLogContainsSafeOutcome(string log, string request, string response)
    {
        Assert.Contains("logging-request", log, StringComparison.Ordinal);
        Assert.Contains("engine.checkStatus", log, StringComparison.Ordinal);
        Assert.Contains("protocol.unsupportedVersion", log, StringComparison.Ordinal);
        Assert.Contains(" in ", log, StringComparison.Ordinal);
        Assert.Contains(" ms.", log, StringComparison.Ordinal);
        Assert.DoesNotContain(request, log, StringComparison.Ordinal);
        Assert.DoesNotContain(response, log, StringComparison.Ordinal);
    }

    private static string FirstErrorCode(JsonDocument response) =>
        response.RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;

    private static string FixturePath(string fileName) =>
        Path.Combine(RepositoryPaths.EngineProtocolV1, "fixtures", fileName);

    private static async Task<JsonDocument> ReadResponseAsync(Process engine) =>
        JsonDocument.Parse(await ReadResponseLineAsync(engine));

    private static async Task<string> ReadResponseLineAsync(Process engine)
    {
        var responseLine = await engine.StandardOutput
            .ReadLineAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(responseLine));
        return responseLine;
    }

    private static Task WaitForExitAsync(Process engine) =>
        engine.WaitForExitAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

    private static Process StartEngine(
        string? logDirectory = null,
        bool redirectStandardError = false)
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
            RedirectStandardError = redirectStandardError,
            UseShellExecute = false,
        };

        if (logDirectory is not null)
        {
            startInfo.Environment["ChangeLens__Logging__FileDirectory"] = logDirectory;
            startInfo.Environment["Serilog__MinimumLevel__Default"] = "Debug";
        }

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(engineProject);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The ChangeLens engine process could not be started.");
    }
}
