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
    ///     Verifies that the engine returns version information correlated to a versioned request.
    /// </summary>
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
    /// <param name="expectedCode">The stable error code expected in the response.</param>
    [Theory]
    [InlineData("not-json", "protocol.invalidJson")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"\",\"method\":\"engine.getInfo\"}",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"requestId\":\"request-missing-version\",\"method\":\"engine.getInfo\"}",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"method\":\"engine.getInfo\"}",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-missing-method\"}",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-extra\",\"method\":\"engine.getInfo\",\"extra\":true}",
        "protocol.invalidRequest")]
    [InlineData(
        "{\"protocolVersion\":2,\"requestId\":\"request-2\",\"method\":\"engine.getInfo\"}",
        "protocol.unsupportedVersion")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-3\",\"method\":\"unknown\"}",
        "protocol.unknownMethod")]
    public async Task EngineReturnsStructuredErrorForKnownProtocolFailure(
        string request,
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
        Assert.Equal(expectedCode, root.GetProperty("error").GetProperty("code").GetString());
    }

    private static Process StartEngine()
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
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(engineProject);

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The ChangeLens engine process could not be started.");

        return process;
    }

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
