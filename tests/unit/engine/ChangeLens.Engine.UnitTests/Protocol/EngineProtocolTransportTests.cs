using ChangeLens.Core.Results.Models;
using System.Text.Json;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.UnitTests.Support;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies bounded protocol input and exactly-once protocol output.
/// </summary>
public sealed class EngineProtocolTransportTests
{
    private const string ValidRequest =
        "{\"protocolVersion\":1,\"requestId\":\"request-1\",\"action\":\"engine.checkStatus\"}";

    /// <summary>
    ///     Verifies that one complete line is decoded into a request.
    /// </summary>
    [Fact]
    public async Task ReadAsyncReturnsDecodedRequest()
    {
        var transport = CreateTransport(new StringReader(ValidRequest));

        var result = await transport.ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("request-1", result.Data!.RequestId);
        Assert.Equal("engine.checkStatus", result.Data.Action);
    }

    /// <summary>
    ///     Verifies that closed input returns successful end-of-input.
    /// </summary>
    [Fact]
    public async Task ReadAsyncReturnsSuccessfulNullWhenInputCloses()
    {
        var transport = CreateTransport(new StringReader(string.Empty));

        var result = await transport.ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data);
    }

    /// <summary>
    ///     Verifies that an input line at the exact character limit is accepted by the bounded reader.
    /// </summary>
    [Fact]
    public async Task ReadAsyncAcceptsLineAtCharacterLimit()
    {
        var line = ValidRequest.PadRight(65_536, ' ');
        var transport = CreateTransport(new StringReader(line));

        var result = await transport.ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("engine.checkStatus", result.Data!.Action);
    }

    /// <summary>
    ///     Verifies that oversized input is drained without consuming the following request.
    /// </summary>
    [Fact]
    public async Task ReadAsyncRejectsAndDrainsLineOverCharacterLimit()
    {
        var transport = CreateTransport(new StringReader(new string('x', 65_537) + "\n" + ValidRequest));

        var rejected = await transport.ReadAsync(TestContext.Current.CancellationToken);
        var accepted = await transport.ReadAsync(TestContext.Current.CancellationToken);

        var error = Assert.Single(rejected.Errors);
        Assert.Equal(ErrorType.MalformedInput, error.Type);
        Assert.Equal("protocol.requestTooLarge", error.Code);
        Assert.True(accepted.IsSuccess);
        Assert.Equal("engine.checkStatus", accepted.Data!.Action);
    }

    /// <summary>
    ///     Verifies that a known reader failure becomes an external-dependency failure.
    /// </summary>
    [Fact]
    public async Task ReadAsyncReturnsExternalDependencyFailureForReaderFailure()
    {
        var transport = CreateTransport(new ThrowingTextReader(new IOException("reader sensitive")));

        var result = await transport.ReadAsync(TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal("protocol.readFailed", error.Code);
    }

    /// <summary>
    ///     Verifies that cancellation from the supplied token is not converted to Result data.
    /// </summary>
    [Fact]
    public async Task ReadAsyncPreservesCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var transport = CreateTransport(new BlockingTextReader());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transport.ReadAsync(source.Token));
    }

    /// <summary>
    ///     Verifies that one response is written as one line and flushed once.
    /// </summary>
    [Fact]
    public async Task WriteAsyncWritesAndFlushesOneResponse()
    {
        var output = new ThrowingTextWriter();
        var transport = CreateTransport(TextReader.Null, output);
        var response = ProtocolResponseFactory.FromResult("request-1", Result.Success());

        var result = await transport.WriteAsync(response, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, output.FlushCount);
        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var line = Assert.Single(lines);
        using var document = JsonDocument.Parse(line);
        Assert.Equal(1, document.RootElement.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("result", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("request-1", document.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("result").ValueKind);
    }

    /// <summary>
    ///     Verifies that serializer failure is forwarded without attempting output.
    /// </summary>
    [Fact]
    public async Task WriteAsyncReturnsSerializationFailure()
    {
        var output = new ThrowingTextWriter();
        var transport = CreateTransport(TextReader.Null, output);
        var response = ProtocolResponseFactory.CreateWithValue("request-1", (Action)(() => { }));

        var result = await transport.WriteAsync(response, TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal("protocol.serializationFailed", error.Code);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(0, output.FlushCount);
    }

    /// <summary>
    ///     Verifies that a writer failure becomes an external-dependency failure.
    /// </summary>
    [Fact]
    public async Task WriteAsyncReturnsExternalDependencyFailureForWriteFailure()
    {
        var transport = CreateTransport(
            TextReader.Null,
            new ThrowingTextWriter(writeException: new IOException("write sensitive")));

        var result = await transport.WriteAsync(
            ProtocolResponseFactory.FromResult("request-1", Result.Success()),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal("protocol.writeFailed", error.Code);
    }

    /// <summary>
    ///     Verifies that a flush failure becomes an external-dependency failure.
    /// </summary>
    [Fact]
    public async Task WriteAsyncReturnsExternalDependencyFailureForFlushFailure()
    {
        var transport = CreateTransport(
            TextReader.Null,
            new ThrowingTextWriter(flushException: new IOException("flush sensitive")));

        var result = await transport.WriteAsync(
            ProtocolResponseFactory.FromResult("request-1", Result.Success()),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal("protocol.writeFailed", error.Code);
    }

    private static IEngineProtocolTransport CreateTransport(
        TextReader input,
        TextWriter? output = null) =>
        new EngineProtocolTransport(
            input,
            output ?? TextWriter.Null,
            new EngineProtocolSerializer(),
            new TestLogger<EngineProtocolTransport>());
}
