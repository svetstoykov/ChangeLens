using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.UnitTests.EngineStatus.Support;
using ChangeLens.Engine.UnitTests.Support;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies typed action selection and Core Result mapping.
/// </summary>
public sealed class EngineActionProcessorTests
{
    /// <summary>
    ///     Verifies that status success returns a correlated payload-free result.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncReturnsPayloadFreeStatusResult()
    {
        var processor = CreateProcessor(_ => Task.FromResult(Result.Success()));

        var response = await processor.ProcessAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<JsonElement?>>(response);
        Assert.Equal("request-1", result.RequestId);
        Assert.Null(result.Result);
    }

    /// <summary>
    ///     Verifies that status rejects every supplied parameters value without calling Core.
    /// </summary>
    /// <param name="parametersJson">The explicitly supplied parameters value.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("\"value\"")]
    public async Task ProcessAsyncRejectsEverySuppliedStatusParametersValue(string parametersJson)
    {
        var callCount = 0;
        var processor = CreateProcessor(
            _ =>
            {
                callCount++;
                return Task.FromResult(Result.Success());
            });

        var response = await processor.ProcessAsync(
            CreateRequest(parameters: Parse(parametersJson)),
            TestContext.Current.CancellationToken);

        var errorResponse = Assert.IsType<ProtocolErrorResponse>(response);
        var error = Assert.Single(errorResponse.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("protocol.invalidRequest", error.Code);
        Assert.Equal(0, callCount);
    }

    /// <summary>
    ///     Verifies that unsupported versions return the stable unprocessable-input error.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncRejectsUnsupportedVersion()
    {
        var processor = CreateProcessor(_ => Task.FromResult(Result.Success()));

        var response = await processor.ProcessAsync(
            CreateRequest(protocolVersion: 2),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.UnprocessableInput, error.Type);
        Assert.Equal("protocol.unsupportedVersion", error.Code);
    }

    /// <summary>
    ///     Verifies that an unknown action returns the stable not-found error.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncRejectsUnknownAction()
    {
        var processor = CreateProcessor(_ => Task.FromResult(Result.Success()));

        var response = await processor.ProcessAsync(
            CreateRequest(action: "analysis.run"),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("protocol.unknownAction", error.Code);
    }

    /// <summary>
    ///     Verifies that a known Core failure is preserved at the protocol boundary.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncPreservesCoreFailure()
    {
        var sourceError = OperationError.Conflict("Status conflict.", "status.conflict");
        var processor = CreateProcessor(_ => Task.FromResult(Result.Fail(sourceError)));

        var response = await processor.ProcessAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(sourceError.Type, error.Type);
        Assert.Equal(sourceError.Code, error.Code);
        Assert.Equal(sourceError.Message, error.Message);
    }

    /// <summary>
    ///     Verifies that unexpected capability exceptions are logged once and sanitized.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncSanitizesUnexpectedCapabilityException()
    {
        var logger = new TestLogger<EngineActionProcessor>();
        var processor = CreateProcessor(
            _ => throw new InvalidOperationException("sensitive capability detail"),
            logger);

        var response = await processor.ProcessAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal("engine.unexpectedFailure", error.Code);
        Assert.DoesNotContain("sensitive", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, logger.ErrorCount);
        Assert.IsType<InvalidOperationException>(logger.LastException);
    }

    /// <summary>
    ///     Verifies that capability cancellation from the supplied token remains exception-based.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncPreservesCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var processor = CreateProcessor(
            token => Task.FromCanceled<Result>(token));

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => processor.ProcessAsync(CreateRequest(), source.Token));
    }

    private static EngineActionProcessor CreateProcessor(
        Func<CancellationToken, Task<Result>> checkStatusAsync,
        TestLogger<EngineActionProcessor>? logger = null) =>
        new(
            new StubEngineStatusService(checkStatusAsync),
            logger ?? new TestLogger<EngineActionProcessor>());

    private static EngineProtocolRequest CreateRequest(
        int protocolVersion = 1,
        string action = "engine.checkStatus",
        JsonElement parameters = default) =>
        new()
        {
            ProtocolVersion = protocolVersion,
            RequestId = "request-1",
            Action = action,
            Parameters = parameters,
        };

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
