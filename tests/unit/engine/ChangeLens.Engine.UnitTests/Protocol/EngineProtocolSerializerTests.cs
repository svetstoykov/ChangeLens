using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.EngineInformation.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies strict serialization and deserialization for the engine protocol.
/// </summary>
public sealed class EngineProtocolSerializerTests
{
    private readonly EngineProtocolSerializer _serializer = new();

    /// <summary>
    ///     Verifies that a valid engine-information request is bound to the common request envelope.
    /// </summary>
    [Fact]
    public void DeserializeRequestBindsEngineProtocolRequest()
    {
        var result = this._serializer.DeserializeRequest(
            """{"protocolVersion":1,"requestId":"request-bind","method":"engine.getInfo","params":{}}""");

        Assert.True(result.IsSuccess);
        var request = Assert.IsType<EngineProtocolRequest>(result.Data);
        Assert.Equal(1, request.ProtocolVersion);
        Assert.Equal("request-bind", request.RequestId);
        Assert.Equal(EngineProtocolConstants.GetInformationMethod, request.Method);
        Assert.Equal(JsonValueKind.Object, request.Params!.Value.ValueKind);
    }

    /// <summary>
    ///     Verifies that syntactically malformed JSON returns the stable invalid-request failure.
    /// </summary>
    [Fact]
    public void DeserializeRequestReturnsInvalidRequestForInvalidJson()
    {
        var result = this._serializer.DeserializeRequest("not-json");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(EngineProtocolConstants.InvalidRequestErrorCode, error.Code);
    }

    /// <summary>
    ///     Verifies that valid JSON values with invalid request shapes return the stable validation failure.
    /// </summary>
    /// <param name="requestLine">The request line that violates the schema.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"first\",\"requestId\":\"second\",\"method\":\"engine.getInfo\",\"params\":{}}")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-duplicate\",\"method\":\"engine.getInfo\",\"method\":\"engine.getInfo\",\"params\":{}}")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"request-extra\",\"method\":\"engine.getInfo\",\"params\":{},\"extra\":true}")]
    [InlineData(
        "{\"protocolVersion\":\"1\",\"requestId\":\"request-version\",\"method\":\"engine.getInfo\",\"params\":{}}")]
    [InlineData(
        "{\"protocolVersion\":1,\"requestId\":\"\",\"method\":\"engine.getInfo\",\"params\":{}}")]
    public void DeserializeRequestRejectsInvalidRequestShape(string requestLine)
    {
        var result = this._serializer.DeserializeRequest(requestLine);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(EngineProtocolConstants.InvalidRequestErrorCode, error.Code);
    }

    /// <summary>
    ///     Verifies that a parameter-free action can omit the params property.
    /// </summary>
    [Fact]
    public void DeserializeRequestAllowsMissingParams()
    {
        var result = this._serializer.DeserializeRequest(
            """{"protocolVersion":1,"requestId":"request-without-params","method":"engine.getInfo"}""");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Params);
    }

    /// <summary>
    ///     Verifies that an unrecognized method remains a valid common request envelope.
    /// </summary>
    [Fact]
    public void DeserializeRequestBindsUnrecognizedMethod()
    {
        var result = this._serializer.DeserializeRequest(
            """{"protocolVersion":1,"requestId":"request-method","method":"analysis.run","params":{}}""");

        Assert.True(result.IsSuccess);
        Assert.Equal("analysis.run", result.Data!.Method);
    }

    /// <summary>
    ///     Verifies that action parameters use the same strict JSON policy as the common request envelope.
    /// </summary>
    [Fact]
    public void DeserializeParametersRejectsUnknownProperties()
    {
        using var document = JsonDocument.Parse("""{"repositoryId":"repository-1"}""");

        var result = this._serializer.DeserializeParameters<EngineInformationParameters>(
            document.RootElement,
            EngineProtocolConstants.GetInformationMethod);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(EngineProtocolConstants.InvalidRequestErrorCode, error.Code);
    }

    /// <summary>
    ///     Verifies that response serialization uses the shared protocol enum representation.
    /// </summary>
    [Fact]
    public void SerializeResponseUsesSharedProtocolOptions()
    {
        var response = ProtocolResponseFactory.CreateError(
            "request-error",
            [OperationError.Validation("Invalid.", "fixture.invalid")]);

        var json = this._serializer.SerializeResponse(response);

        Assert.Contains("\"type\":\"Validation\"", json, StringComparison.Ordinal);
    }
}
