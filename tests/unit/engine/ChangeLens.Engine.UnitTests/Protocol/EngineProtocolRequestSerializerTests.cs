using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies strict deserialization and common-envelope binding for the engine protocol.
/// </summary>
public sealed class EngineProtocolRequestSerializerTests
{
    private readonly EngineProtocolRequestSerializer _serializer = new();

    /// <summary>
    ///     Verifies that a valid engine-information request is bound to the common request envelope.
    /// </summary>
    [Fact]
    public void DeserializeBindsEngineProtocolRequest()
    {
        var result = this._serializer.Deserialize(
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
    public void DeserializeReturnsInvalidRequestForInvalidJson()
    {
        var result = this._serializer.Deserialize("not-json");

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
    public void DeserializeRejectsInvalidRequestShape(string requestLine)
    {
        var result = this._serializer.Deserialize(requestLine);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(EngineProtocolConstants.InvalidRequestErrorCode, error.Code);
    }

    /// <summary>
    ///     Verifies that a parameter-free action can omit the params property.
    /// </summary>
    [Fact]
    public void DeserializeAllowsMissingParams()
    {
        var result = this._serializer.Deserialize(
            """{"protocolVersion":1,"requestId":"request-without-params","method":"engine.getInfo"}""");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Params);
    }

    /// <summary>
    ///     Verifies that an unrecognized method remains a valid common request envelope.
    /// </summary>
    [Fact]
    public void DeserializeBindsUnrecognizedMethod()
    {
        var result = this._serializer.Deserialize(
            """{"protocolVersion":1,"requestId":"request-method","method":"analysis.run","params":{}}""");

        Assert.True(result.IsSuccess);
        Assert.Equal("analysis.run", result.Data!.Method);
    }
}
