using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Models;
using ChangeLens.Engine.UnitTests.EngineStatus.Support;
using ChangeLens.Engine.UnitTests.Repositories.Support;
using ChangeLens.Engine.UnitTests.Support;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies typed action selection and Core Result mapping.
/// </summary>
public sealed class EngineActionProcessorTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";

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

    /// <summary>
    ///     Verifies that repository open rejects parameters that do not match its strict schema before Core I/O.
    /// </summary>
    /// <param name="parametersJson">
    ///     The malformed parameters JSON, or <see langword="null" /> when parameters are omitted.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("\"value\"")]
    [InlineData("{}")]
    [InlineData("""{"extra":true}""")]
    [InlineData("""{"path":null}""")]
    [InlineData("""{"path":1}""")]
    [InlineData("""{"Path":"/selected"}""")]
    [InlineData("""{"path":"/first","path":"/second"}""")]
    public async Task ProcessAsyncRejectsMalformedRepositoryParametersBeforeCoreIo(string? parametersJson)
    {
        var fixture = new RepositoryInspectorFixture();
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRequest(
                action: "repositories.open",
                parameters: parametersJson is null ? default : Parse(parametersJson)),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("protocol.invalidRequest", error.Code);
        Assert.Empty(fixture.Paths);
        Assert.Empty(fixture.Commands);
    }

    /// <summary>
    ///     Verifies that structurally valid path strings are validated by Core rather than the common envelope.
    /// </summary>
    /// <param name="path">The path value rejected by Core.</param>
    [Theory]
    [InlineData(" \t\r\n")]
    [InlineData("/repository\u0000child")]
    public async Task ProcessAsyncLetsCoreRejectStructurallyValidRepositoryPaths(string path)
    {
        var fixture = new RepositoryInspectorFixture();
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest(path),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("repository.invalidPath", error.Code);
        Assert.Empty(fixture.Paths);
        Assert.Empty(fixture.Commands);
    }

    /// <summary>
    ///     Verifies that an overlong structurally bound path is rejected by Core.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncLetsCoreRejectPathLongerThan8192Scalars()
    {
        var fixture = new RepositoryInspectorFixture();
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest(new string('a', 8_193)),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("repository.invalidPath", error.Code);
        Assert.Empty(fixture.Paths);
        Assert.Empty(fixture.Commands);
    }

    /// <summary>
    ///     Verifies that attached repository state maps to the exact typed protocol result.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncMapsBranchRepositoryResult()
    {
        var fixture = new RepositoryInspectorFixture();
        fixture.EnqueueSuccessfulInspection(Revision, "main");
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest("/selected"),
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<RepositoryOpenResult>>(response);
        Assert.Equal(
            new RepositoryOpenResult(
                new RepositoryResult(
                    "change_lens",
                    "/projects/change_lens",
                    new BranchRepositoryHeadResult("main", Revision))),
            result.Result);
    }

    /// <summary>
    ///     Verifies that detached repository state maps to the exact typed protocol result.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncMapsDetachedRepositoryResult()
    {
        var fixture = new RepositoryInspectorFixture();
        fixture.EnqueueSuccessfulInspection(Revision, branchName: null);
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest("/selected"),
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<RepositoryOpenResult>>(response);
        Assert.Equal(
            new RepositoryOpenResult(
                new RepositoryResult(
                    "change_lens",
                    "/projects/change_lens",
                    new DetachedRepositoryHeadResult(Revision))),
            result.Result);
    }

    /// <summary>
    ///     Verifies that a Core repository failure keeps its category, code, and message.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncPreservesRepositoryCoreFailure()
    {
        var sourceError = OperationError.Unauthorized(
            "Repository access was denied.",
            "repository.accessDenied");
        var fixture = new RepositoryInspectorFixture();
        fixture.EnqueuePath(Result.Fail<string>(sourceError));
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest("/selected"),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(sourceError.Type, error.Type);
        Assert.Equal(sourceError.Code, error.Code);
        Assert.Equal(sourceError.Message, error.Message);
    }

    /// <summary>
    ///     Verifies that repository cancellation remains exception-based at the Engine boundary.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncPreservesRepositoryCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var fixture = new RepositoryInspectorFixture();
        fixture.EnqueuePath(
            (_, token) => Task.FromCanceled<Result<string>>(token));
        var processor = CreateProcessor(fixture: fixture);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ProcessAsync(CreateRepositoryRequest("/selected"), source.Token));
    }

    /// <summary>
    ///     Verifies that unexpected repository collaborator failures are logged once and sanitized.
    /// </summary>
    /// <param name="throwFromRunner">
    ///     <see langword="true" /> to throw from Git execution; otherwise, throw from path resolution.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsyncSanitizesUnexpectedRepositoryException(bool throwFromRunner)
    {
        var fixture = new RepositoryInspectorFixture();
        if (throwFromRunner)
        {
            fixture.EnqueuePath(Result.Success<string>("/physical/selection"));
            fixture.EnqueueCommand(
                (_, _) => throw new InvalidOperationException("sensitive Git detail"));
        }
        else
        {
            fixture.EnqueuePath(
                (_, _) => throw new InvalidOperationException("sensitive path detail"));
        }

        var logger = new TestLogger<EngineActionProcessor>();
        var processor = CreateProcessor(fixture: fixture, logger: logger);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest("/selected"),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal("engine.unexpectedFailure", error.Code);
        Assert.DoesNotContain("sensitive", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, logger.ErrorCount);
        Assert.IsType<InvalidOperationException>(logger.LastException);
    }

    private static EngineActionProcessor CreateProcessor(
        Func<CancellationToken, Task<Result>>? checkStatusAsync = null,
        TestLogger<EngineActionProcessor>? logger = null,
        RepositoryInspectorFixture? fixture = null)
    {
        fixture ??= new RepositoryInspectorFixture();
        return new EngineActionProcessor(
            new StubEngineStatusService(checkStatusAsync ?? (_ => Task.FromResult(Result.Success()))),
            fixture.Inspector,
            new EngineProtocolSerializer(),
            logger ?? new TestLogger<EngineActionProcessor>());
    }

    private static EngineProtocolRequest CreateRepositoryRequest(string path) =>
        CreateRequest(
            action: "repositories.open",
            parameters: Parse(JsonSerializer.Serialize(new { path })));

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
