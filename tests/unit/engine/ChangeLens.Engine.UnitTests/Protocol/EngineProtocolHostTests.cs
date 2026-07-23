using ChangeLens.Core.Results.Models;
using System.Text.Json;
using ChangeLens.Engine.Hosting.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.UnitTests.EngineStatus.Support;
using ChangeLens.Engine.UnitTests.Repositories.Support;
using ChangeLens.Engine.UnitTests.Support;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies lifecycle orchestration around protocol transport and action processing.
/// </summary>
[Collection(EngineExitCodeCollection.Name)]
public sealed class EngineProtocolHostTests
{
    /// <summary>
    ///     Verifies that host shutdown cancellation stops protocol input normally.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task StopAsyncCancelsProtocolInputWithoutAnErrorLog()
    {
        var readStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new StubEngineProtocolTransport(
            async cancellationToken =>
            {
                readStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Result.Success<EngineProtocolRequest?>(null);
            },
            (_, _) => Task.FromResult(Result.Success()));
        var logger = new TestLogger<EngineProtocolHost>();
        var lifetime = new TestHostApplicationLifetime();
        var service = CreateService(
            transport,
            logger,
            lifetime,
            _ => Task.FromResult(Result.Success()));

        await service.StartAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await readStarted.Task.WaitAsync(timeout.Token);
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

    /// <summary>
    ///     Verifies that a fatal read failure is written once before the host exits unsuccessfully.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task HostWritesFatalReadFailureAndStopsWithNonzeroExitCode()
    {
        var originalExitCode = Environment.ExitCode;
        var readError = OperationError.ExternalDependencyFailure(
            "Input failed.",
            "protocol.readFailed");
        ProtocolResponse? writtenResponse = null;
        var transport = new StubEngineProtocolTransport(
            _ => Task.FromResult(Result.Fail<EngineProtocolRequest?>(readError)),
            (response, _) =>
            {
                writtenResponse = response;
                return Task.FromResult(Result.Success());
            });
        var statusCallCount = 0;
        var logger = new TestLogger<EngineProtocolHost>();
        var lifetime = new TestHostApplicationLifetime();
        var service = CreateService(
            transport,
            logger,
            lifetime,
            _ =>
            {
                statusCallCount++;
                return Task.FromResult(Result.Success());
            });

        try
        {
            Environment.ExitCode = 0;
            await RunUntilStoppedAsync(service, lifetime);

            Assert.Equal(EngineProcessConstants.UnexpectedFailureExitCode, Environment.ExitCode);
            Assert.Equal(1, transport.ReadCount);
            Assert.Equal(1, transport.WriteCount);
            Assert.Equal(0, statusCallCount);
            var error = Assert.Single(
                Assert.IsType<ProtocolErrorResponse>(writtenResponse).Errors);
            Assert.Equal(readError.Type, error.Type);
            Assert.Equal(readError.Code, error.Code);
            Assert.Equal(readError.Message, error.Message);
            Assert.True(lifetime.StopRequested);
            Assert.Equal(1, logger.ErrorCount);
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
    }

    /// <summary>
    ///     Verifies that a write failure stops the host before another request is read or processed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task HostStopsAfterWriteFailureWithoutReadingOrProcessingAnotherRequest()
    {
        var originalExitCode = Environment.ExitCode;
        var transport = new StubEngineProtocolTransport(
            _ => Task.FromResult(
                Result.Success<EngineProtocolRequest?>(CreateRequest("request-first"))),
            (_, _) => Task.FromResult(
                Result.Fail(
                    OperationError.ExternalDependencyFailure(
                        "Output failed.",
                        "protocol.writeFailed"))));
        var statusCallCount = 0;
        var logger = new TestLogger<EngineProtocolHost>();
        var lifetime = new TestHostApplicationLifetime();
        var service = CreateService(
            transport,
            logger,
            lifetime,
            _ =>
            {
                statusCallCount++;
                return Task.FromResult(Result.Success());
            });

        try
        {
            Environment.ExitCode = 0;
            await RunUntilStoppedAsync(service, lifetime);

            Assert.Equal(EngineProcessConstants.UnexpectedFailureExitCode, Environment.ExitCode);
            Assert.Equal(1, transport.ReadCount);
            Assert.Equal(1, transport.WriteCount);
            Assert.Equal(1, statusCallCount);
            Assert.True(lifetime.StopRequested);
            Assert.Equal(1, logger.ErrorCount);
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
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
        return CreateService(
            transport,
            logger,
            lifetime,
            _ => Task.FromResult(Result.Success()));
    }

    private static EngineProtocolHost CreateService(
        IEngineProtocolTransport transport,
        TestLogger<EngineProtocolHost> logger,
        TestHostApplicationLifetime lifetime,
        Func<CancellationToken, Task<Result>> checkStatusAsync) =>
        new(
            transport,
            new EngineActionProcessor(
                new StubEngineStatusService(checkStatusAsync),
                new RepositoryInspectorFixture().Inspector,
                new EngineProtocolSerializer(),
                new TestLogger<EngineActionProcessor>()),
            logger,
            lifetime);

    private static EngineProtocolRequest CreateRequest(string requestId) =>
        new()
        {
            ProtocolVersion = 1,
            RequestId = requestId,
            Action = "engine.checkStatus",
        };

    private static async Task RunUntilStoppedAsync(
        EngineProtocolHost service,
        TestHostApplicationLifetime lifetime)
    {
        await service.StartAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await lifetime.WaitForStopAsync(timeout.Token);
        await service.StopAsync(timeout.Token);
    }
}
