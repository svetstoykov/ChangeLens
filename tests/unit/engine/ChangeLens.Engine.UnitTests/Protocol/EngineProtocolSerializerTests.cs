using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Models;
using ChangeLens.Engine.UnitTests.Support;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies strict serialization and deserialization for the engine protocol.
/// </summary>
public sealed class EngineProtocolSerializerTests
{
    private readonly EngineProtocolSerializer _serializer = new();

    /// <summary>
    ///     Verifies that a valid action request binds to the common request envelope.
    /// </summary>
    [Fact]
    public void DeserializeRequestBindsActionEnvelope()
    {
        var result = _serializer.DeserializeRequest(
            """{"protocolVersion":1,"requestId":"request-bind","action":"engine.checkStatus","parameters":{}}""");

        Assert.True(result.IsSuccess);
        var request = Assert.IsType<EngineProtocolRequest>(result.Data);
        Assert.Equal(1, request.ProtocolVersion);
        Assert.Equal("request-bind", request.RequestId);
        Assert.Equal("engine.checkStatus", request.Action);
        Assert.Equal(JsonValueKind.Object, request.Parameters.ValueKind);
    }

    /// <summary>
    ///     Verifies that omitted parameters remain distinguishable from supplied values.
    /// </summary>
    [Fact]
    public void DeserializeRequestKeepsMissingParametersUndefined()
    {
        var result = _serializer.DeserializeRequest(
            """{"protocolVersion":1,"requestId":"request-omitted","action":"engine.checkStatus"}""");

        Assert.True(result.IsSuccess);
        Assert.Equal(JsonValueKind.Undefined, result.Data!.Parameters.ValueKind);
    }

    /// <summary>
    ///     Verifies that explicit JSON null parameters remain present.
    /// </summary>
    [Fact]
    public void DeserializeRequestKeepsExplicitNullParametersPresent()
    {
        var result = _serializer.DeserializeRequest(
            """{"protocolVersion":1,"requestId":"request-null","action":"engine.checkStatus","parameters":null}""");

        Assert.True(result.IsSuccess);
        Assert.Equal(JsonValueKind.Null, result.Data!.Parameters.ValueKind);
    }

    /// <summary>
    ///     Verifies that syntactically malformed JSON returns the stable malformed-input failure.
    /// </summary>
    [Fact]
    public void DeserializeRequestReturnsMalformedInputForInvalidJson()
    {
        var result = _serializer.DeserializeRequest("not-json");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.MalformedInput, error.Type);
        Assert.Equal("protocol.invalidJson", error.Code);
    }

    /// <summary>
    ///     Verifies that valid JSON with an invalid envelope returns stable validation failure.
    /// </summary>
    /// <param name="requestLine">The request line that violates the envelope schema.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"one\",\"requestId\":\"two\"," +
        "\"action\":\"engine.checkStatus\"}")]
    [InlineData("{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"engine.checkStatus\",\"action\":\"other\"}")]
    [InlineData("{\"protocolVersion\":\"1\",\"requestId\":\"id\",\"action\":\"engine.checkStatus\"}")]
    [InlineData("{\"protocolVersion\":1,\"requestId\":\"\",\"action\":\"engine.checkStatus\"}")]
    [InlineData("{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"\"}")]
    [InlineData("{\"protocolVersion\":1,\"requestId\":\"id\",\"action\":\"engine.checkStatus\",\"extra\":true}")]
    public void DeserializeRequestRejectsInvalidEnvelopeShape(string requestLine)
    {
        var result = _serializer.DeserializeRequest(requestLine);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("protocol.invalidRequest", error.Code);
    }

    /// <summary>
    ///     Verifies that typed parameters reject properties outside their schema.
    /// </summary>
    [Fact]
    public void DeserializeParametersRejectsUnknownProperties()
    {
        using var document = JsonDocument.Parse("""{"repositoryId":"repository-1"}""");

        var result = _serializer.DeserializeParameters<FixturePayload>(
            document.RootElement,
            "engine.checkStatus");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("protocol.invalidRequest", error.Code);
    }

    /// <summary>
    ///     Verifies that repository parameters reject malformed nested properties.
    /// </summary>
    /// <param name="parametersJson">The malformed repository parameters.</param>
    [Theory]
    [InlineData("""{"path":"/first","path":"/second"}""")]
    [InlineData("""{"Path":"/repository"}""")]
    [InlineData("""{"path":"/repository","extra":true}""")]
    [InlineData("""{"path":null}""")]
    public void DeserializeParametersRejectsMalformedRepositoryProperties(string parametersJson)
    {
        using var document = JsonDocument.Parse(parametersJson);

        var result = _serializer.DeserializeParameters<RepositoryOpenParameters>(
            document.RootElement,
            "repositories.open");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("protocol.invalidRequest", error.Code);
    }

    /// <summary>
    ///     Verifies that repository parameter names use the exact protocol casing.
    /// </summary>
    [Fact]
    public void DeserializeParametersBindsExactRepositoryPathProperty()
    {
        using var document = JsonDocument.Parse("""{"path":"/repository"}""");

        var result = _serializer.DeserializeParameters<RepositoryOpenParameters>(
            document.RootElement,
            "repositories.open");

        Assert.True(result.IsSuccess);
        Assert.Equal("/repository", result.Data!.Path);
    }

    /// <summary>
    ///     Verifies that structural binding leaves the path scalar bound to Core validation.
    /// </summary>
    [Fact]
    public void DeserializeParametersBinds8193ScalarRepositoryPath()
    {
        var path = new string('a', 8_193);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { path }));

        var result = _serializer.DeserializeParameters<RepositoryOpenParameters>(
            document.RootElement,
            "repositories.open");

        Assert.True(result.IsSuccess);
        Assert.Equal(path, result.Data!.Path);
    }

    /// <summary>
    ///     Verifies that concrete response types serialize with the protocol JSON policy.
    /// </summary>
    [Fact]
    public void SerializeResponseReturnsTypedJson()
    {
        var response = ProtocolResponseFactory.CreateError(
            "request-error",
            [OperationError.Validation("Invalid.", "fixture.invalid")]);

        var result = _serializer.SerializeResponse(response);

        Assert.True(result.IsSuccess);
        Assert.Contains("\"type\":\"Validation\"", result.Data, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that unsupported response payloads return a stable serialization failure.
    /// </summary>
    [Fact]
    public void SerializeResponseReturnsFailureForUnsupportedPayload()
    {
        var response = ProtocolResponseFactory.CreateWithValue("request-unsupported", (Action)(() => { }));

        var result = _serializer.SerializeResponse(response);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal("protocol.serializationFailed", error.Code);
    }
}
