using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies the engine-information exchange through the standard-input/output protocol boundary.
/// </summary>
public sealed class EngineInformationProtocolTests
{
    /// <summary>
    ///     Verifies that protocol traffic is logged without writing diagnostics to standard output.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task EngineLogsProtocolTrafficWithoutCorruptingStandardOutput()
    {
        const string request =
            """
            {"protocolVersion":1,"requestId":"logging-request","method":"engine.getInfo"}
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
                "Processed protocol request logging-request for engine.getInfo with a result",
                standardError);

            var logFile = Assert.Single(
                Directory.GetFiles(logDirectory, "changelens-engine-*.log"));
            var fileLog = await File.ReadAllTextAsync(
                logFile,
                TestContext.Current.CancellationToken);

            Assert.Contains($"Received protocol request from stdin: {request}", fileLog);
            Assert.Contains($"Wrote protocol response to stdout: {responseLine}", fileLog);
            Assert.Contains(
                "Processed protocol request logging-request for engine.getInfo with a result",
                fileLog);
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

        var responseLine = await engine.StandardOutput
            .ReadLineAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(responseLine));

        using var response = JsonDocument.Parse(responseLine);
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
    [InlineData("not-json", "MalformedInput", "protocol.invalidJson")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"\",\"method\":\"engine.getInfo\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"requestId\":\"request-missing-version\",\"method\":\"engine.getInfo\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"method\":\"engine.getInfo\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-missing-method\"}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-extra\",\"method\":\"engine.getInfo\",\"extra\":true}",
        "Validation",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":2,\"requestId\":\"request-2\",\"method\":\"engine.getInfo\"}",
        "UnprocessableInput",
        "protocol.unsupportedVersion")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-3\",\"method\":\"unknown\"}",
        "NotFound",
        "protocol.unknownMethod")]
    public async Task EngineReturnsStructuredErrorForKnownProtocolFailure(
        string request,
        string expectedType,
        string expectedCode)
    {
        using var engine = StartEngine();

        await engine.StandardInput.WriteLineAsync(request);

        var responseLine = await engine.StandardOutput
            .ReadLineAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(responseLine));

        using var response = JsonDocument.Parse(responseLine);
        var root = response.RootElement;

        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("error", root.GetProperty("type").GetString());
        Assert.Equal(expectedType, root.GetProperty("error").GetProperty("type").GetString());
        Assert.Equal(expectedCode, root.GetProperty("error").GetProperty("code").GetString());
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
        var repositoryRoot = FindRepositoryRoot();
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

    /// <summary>
    ///     Finds the repository root by walking up from the test output directory.
    /// </summary>
    /// <returns>The full path to the repository root.</returns>
    /// <exception cref="DirectoryNotFoundException">The repository root could not be found.</exception>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "engine", "ChangeLens.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The ChangeLens repository root could not be located.");
    }
}
