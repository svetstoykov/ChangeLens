using ChangeLens.Core.Results.Models;
using System.Text.Json;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.UnitTests.EngineStatus.Support;
using ChangeLens.Engine.UnitTests.Support;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies lifecycle orchestration around protocol transport and action processing.
/// </summary>
public sealed class EngineProtocolHostTests
{
    /// <summary>
    ///     Verifies that host shutdown cancellation stops protocol input normally.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task StopAsyncCancelsProtocolInputWithoutAnErrorLog()
    {
        var logger = new TestLogger<EngineProtocolHost>();
        var lifetime = new TestHostApplicationLifetime();
        var service = CreateService(new BlockingTextReader(), TextWriter.Null, logger, lifetime);

        await service.StartAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StopAsync(timeout.Token);
        await lifetime.WaitForStopAsync(timeout.Token);

        Assert.True(lifetime.StopRequested);
        Assert.Equal(0, logger.ErrorCount);
    }

    /// <summary>
    ///     Verifies that rejected input does not prevent the next valid action from completing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task HostProcessesValidInputAfterRejectedInput()
    {
        var input = new StringReader(
            "not-json\n" +
            "{\"protocolVersion\":1,\"requestId\":\"request-valid\",\"action\":\"engine.checkStatus\"}\n");
        var output = new StringWriter();
        var lifetime = new TestHostApplicationLifetime();
        var service = CreateService(input, output, new TestLogger<EngineProtocolHost>(), lifetime);

        await service.StartAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await lifetime.WaitForStopAsync(timeout.Token);
        await service.StopAsync(timeout.Token);

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"code\":\"protocol.invalidJson\"", lines[0], StringComparison.Ordinal);
        using var response = JsonDocument.Parse(lines[1]);
        Assert.Equal("request-valid", response.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(JsonValueKind.Null, response.RootElement.GetProperty("result").ValueKind);
    }

    private static EngineProtocolHost CreateService(
        TextReader input,
        TextWriter output,
        TestLogger<EngineProtocolHost> logger,
        TestHostApplicationLifetime lifetime)
    {
        var serializer = new EngineProtocolSerializer();
        var transport = new EngineProtocolTransport(
            input,
            output,
            serializer,
            new TestLogger<EngineProtocolTransport>());
        var processor = new EngineActionProcessor(
            new StubEngineStatusService(_ => Task.FromResult(Result.Success())),
            new TestLogger<EngineActionProcessor>());

        return new EngineProtocolHost(transport, processor, logger, lifetime);
    }
}
