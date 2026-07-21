using System.Diagnostics;
using System.Text.Json;
using ChangeLens.Engine.IntegrationTests.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies the engine-information exchange through the standard-input/output protocol boundary.
/// </summary>
public sealed class EngineInformationProtocolTests
{
    /// <summary>
    ///     Verifies that the real Engine result matches the shared cross-language fixture.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineInformationResultMatchesSharedFixture()
    {
        var request = await File.ReadAllTextAsync(
            Path.Combine(
                RepositoryPaths.EngineProtocolV1,
                "fixtures",
                "engine-get-info.request.json"),
            TestContext.Current.CancellationToken);
        var expectedJson = await File.ReadAllTextAsync(
            Path.Combine(
                RepositoryPaths.EngineProtocolV1,
                "fixtures",
                "engine-get-info.result.json"),
            TestContext.Current.CancellationToken);
        using var expected = JsonDocument.Parse(expectedJson);
        using var requestDocument = JsonDocument.Parse(request);
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync(
            JsonSerializer.Serialize(requestDocument.RootElement));
        using var actual = await ReadResponseAsync(engine);

        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual.RootElement));
    }

    /// <summary>
    ///     Verifies that the engine exits successfully when its protocol input closes.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineExitsSuccessfullyWhenStandardInputCloses()
    {
        using var engine = StartEngine();

        engine.StandardInput.Close();

        await engine.WaitForExitAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(0, engine.ExitCode);
    }

    /// <summary>
    ///     Verifies that protocol traffic is logged without writing diagnostics to standard output.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineLogsProtocolTrafficWithoutCorruptingStandardOutput()
    {
        const string request =
            """
            {"protocolVersion":2,"requestId":"logging-request","method":"engine.getInfo","params":{}}
            """;
        var logDirectory = Path.Combine(
            Path.GetTempPath(),
            "ChangeLens.Engine.IntegrationTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            using var engine = StartEngine(logDirectory, redirectStandardError: true);

            await engine.StandardInput.WriteLineAsync(request);
            engine.StandardInput.Close();

            var responseLine = await engine.StandardOutput
                .ReadLineAsync(TestContext.Current.CancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.False(string.IsNullOrWhiteSpace(responseLine));
            using var response = JsonDocument.Parse(responseLine);

            await engine.WaitForExitAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            var remainingStandardOutput = await engine.StandardOutput.ReadToEndAsync(
                TestContext.Current.CancellationToken);
            var standardError = await engine.StandardError.ReadToEndAsync(
                TestContext.Current.CancellationToken);

            Assert.Equal(string.Empty, remainingStandardOutput);
            Assert.Contains($"Received protocol request from stdin: {request}", standardError);
            Assert.Contains($"Wrote protocol response to stdout: {responseLine}", standardError);
            Assert.Contains(
                "Processed protocol request logging-request for engine.getInfo with errors",
                standardError);
            Assert.Contains("protocol.unsupportedVersion", standardError);

            var logFile = Assert.Single(
                Directory.GetFiles(logDirectory, "changelens-engine-*.log"));
            var fileLog = await File.ReadAllTextAsync(
                logFile,
                TestContext.Current.CancellationToken);

            Assert.Contains($"Received protocol request from stdin: {request}", fileLog);
            Assert.Contains($"Wrote protocol response to stdout: {responseLine}", fileLog);
            Assert.Contains(
                "Processed protocol request logging-request for engine.getInfo with errors",
                fileLog);
            Assert.Contains("protocol.unsupportedVersion", fileLog);
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
    ///     Verifies that the engine returns version information correlated to a versioned request.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineReturnsInformationForVersionedRequest()
    {
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync(
            """
            {"protocolVersion":1,"requestId":"request-1","method":"engine.getInfo"}
            """);

        using var response = await ReadResponseAsync(engine);
        var root = response.RootElement;

        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("result", root.GetProperty("type").GetString());
        Assert.Equal("request-1", root.GetProperty("requestId").GetString());

        var result = root.GetProperty("result");
        Assert.Equal("ChangeLens.Engine", result.GetProperty("name").GetString());
        Assert.Equal(1, result.GetProperty("protocolVersion").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("version").GetString()));
    }

    /// <summary>
    ///     Verifies that known protocol failures return stable structured errors.
    /// </summary>
    /// <param name="request">The protocol request that produces the known failure.</param>
    /// <param name="expectedType">The broad error category expected in the response.</param>
    /// <param name="expectedCode">The stable error code expected in the response.</param>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Theory]
    [InlineData("not-json", "Validation", "protocol.invalidRequest")]
    [InlineData("null", "Validation", "protocol.invalidRequest")]
    [InlineData("[]", "Validation", "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":\"1\",\"requestId\":\"request-type\",\"method\":\"engine.getInfo\",\"params\":{}}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-duplicate\",\"requestId\":\"request-other\",\"method\":\"engine.getInfo\",\"params\":{}}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"\",\"method\":\"engine.getInfo\",\"params\":{}}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"requestId\":\"request-missing-version\",\"method\":\"engine.getInfo\",\"params\":{}}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"method\":\"engine.getInfo\",\"params\":{}}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-missing-method\",\"params\":{}}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-extra\",\"method\":\"engine.getInfo\",\"params\":{},\"extra\":true}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-parameters\",\"method\":\"engine.getInfo\",\"params\":{},\"parameters\":{}}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-action-field\",\"method\":\"engine.getInfo\",\"params\":{},\"repositoryId\":\"repository-1\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":2,\"requestId\":\"request-2\",\"method\":\"engine.getInfo\",\"params\":{}}",
        "UnprocessableInput",
        "protocol.unsupportedVersion")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-3\",\"method\":\"unknown\",\"params\":{}}",
        "NotFound",
        "protocol.unknownMethod")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"non-object-params\",\"method\":\"engine.getInfo\",\"params\":[]}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"unknown-param\",\"method\":\"engine.getInfo\",\"params\":{\"repositoryId\":\"repository-1\"}}",
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
        var errors = root.GetProperty("errors");
        var error = Assert.Single(errors.EnumerateArray());
        Assert.Equal(expectedType, error.GetProperty("type").GetString());
        Assert.Equal(expectedCode, error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
    }

    /// <summary>
    ///     Verifies that one engine process handles multiple actions sequentially.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineProcessesMultipleSequentialRequests()
    {
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync(
            """{"protocolVersion":1,"requestId":"request-1","method":"engine.getInfo","params":{}}""");
        await engine.StandardInput.WriteLineAsync(
            """{"protocolVersion":1,"requestId":"request-2","method":"engine.getInfo","params":{}}""");
        engine.StandardInput.Close();

        using var first = await ReadResponseAsync(engine);
        using var second = await ReadResponseAsync(engine);

        Assert.Equal("request-1", first.RootElement.GetProperty("requestId").GetString());
        Assert.Equal("request-2", second.RootElement.GetProperty("requestId").GetString());

        await engine.WaitForExitAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, engine.ExitCode);

        var remainingStandardOutput = await engine.StandardOutput.ReadToEndAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, remainingStandardOutput);
    }

    /// <summary>
    ///     Reads one protocol response from the engine process.
    /// </summary>
    /// <param name="engine">The engine process to read. Cannot be <see langword="null" />.</param>
    /// <returns>A parsed response document that the caller must dispose.</returns>
    private static async Task<JsonDocument> ReadResponseAsync(Process engine)
    {
        var responseLine = await engine.StandardOutput
            .ReadLineAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(responseLine));
        return JsonDocument.Parse(responseLine);
    }

    /// <summary>
    ///     Starts the real engine process with redirected protocol streams for a test.
    /// </summary>
    /// <param name="logDirectory">
    ///     The test log directory, or <see langword="null" /> to use the engine default.
    /// </param>
    /// <param name="redirectStandardError">
    ///     <see langword="true" /> to capture engine diagnostics; otherwise, <see langword="false" />.
    /// </param>
    /// <returns>The running engine process.</returns>
    /// <exception cref="InvalidOperationException">The engine process could not be started.</exception>
    private static Process StartEngine(
        string? logDirectory = null,
        bool redirectStandardError = false)
    {
        var repositoryRoot = RepositoryPaths.Root;
        var engineProject = Path.Combine(
            repositoryRoot,
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

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The ChangeLens engine process could not be started.");

        return process;
    }

}
