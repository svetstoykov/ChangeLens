using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.UnitTests.Support;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies transport mapping for direct values and Core Results.
/// </summary>
public sealed class ProtocolResponseFactoryTests
{
    /// <summary>
    ///     Verifies that payload-free mapping returns the shared protocol response abstraction.
    /// </summary>
    [Fact]
    public void FromResultReturnsProtocolResponse()
    {
        ProtocolResponse response = ProtocolResponseFactory.FromResult(
            "request-response",
            Result.Success());

        Assert.IsType<ProtocolResultResponse<object?>>(response);
    }

    /// <summary>
    ///     Verifies that error mapping returns the shared protocol response abstraction.
    /// </summary>
    [Fact]
    public void CreateErrorReturnsProtocolResponse()
    {
        ProtocolResponse response = ProtocolResponseFactory.CreateError(
            "request-error",
            [OperationError.Validation("Invalid.", "fixture.invalid")]);

        Assert.IsType<ProtocolErrorResponse>(response);
    }

    /// <summary>
    ///     Verifies that a direct value remains a typed correlated result.
    /// </summary>
    [Fact]
    public void CreateValueReturnsTypedResult()
    {
        var response = ProtocolResponseFactory.CreateWithValue("request-1", new FixturePayload("value"));

        Assert.Equal(EngineProtocolConstants.CurrentVersion, response.ProtocolVersion);
        Assert.Equal(EngineProtocolConstants.ResultResponseType, response.Type);
        Assert.Equal("request-1", response.RequestId);
        Assert.Equal("value", response.Result.Value);
    }

    /// <summary>
    ///     Verifies that payload-free success serializes a null result.
    /// </summary>
    [Fact]
    public void FromResultReturnsPayloadFreeSuccess()
    {
        var response = ProtocolResponseFactory.FromResult("request-2", Result.Success());

        var result = Assert.IsType<ProtocolResultResponse<object?>>(response);
        Assert.Equal("request-2", result.RequestId);
        Assert.Null(result.Result);
    }

    /// <summary>
    ///     Verifies that typed Result success retains its payload.
    /// </summary>
    [Fact]
    public void FromResultReturnsTypedSuccess()
    {
        var response = ProtocolResponseFactory.FromResult(
            "request-3",
            Result.Success(new FixturePayload("typed")));

        var result = Assert.IsType<ProtocolResultResponse<FixturePayload>>(response);
        Assert.Equal("typed", result.Result.Value);
    }

    /// <summary>
    ///     Verifies that every error is preserved in source order.
    /// </summary>
    [Fact]
    public void CreateErrorPreservesOrderedErrors()
    {
        var first = OperationError.Validation("First.", "fixture.first");
        var second = OperationError.Conflict("Second.", "fixture.second");

        var response = ProtocolResponseFactory.CreateError("request-4", [first, second]);

        Assert.Collection(
            response.Errors,
            error =>
            {
                Assert.Equal(ErrorType.Validation, error.Type);
                Assert.Equal("fixture.first", error.Code);
                Assert.Equal("First.", error.Message);
            },
            error =>
            {
                Assert.Equal(ErrorType.Conflict, error.Type);
                Assert.Equal("fixture.second", error.Code);
                Assert.Equal("Second.", error.Message);
            });
    }

    /// <summary>
    ///     Verifies that ordered-error serialization matches the shared cross-language fixture.
    /// </summary>
    [Fact]
    public void OrderedErrorResponseMatchesSharedFixture()
    {
        var first = OperationError.Validation(
            "The first fixture value is invalid.",
            "fixture.first");
        var second = OperationError.Conflict(
            "The second fixture value conflicts with current state.",
            "fixture.second");
        var response = ProtocolResponseFactory.CreateError("desktop-43", [first, second]);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        using var actual = JsonSerializer.SerializeToDocument(response, options);
        using var expected = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "contracts",
                "engine-protocol",
                "v1",
                "fixtures",
                "ordered-errors.response.json")));

        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual.RootElement));
    }

    /// <summary>
    ///     Verifies that an invalid source error becomes one sanitized internal error.
    /// </summary>
    [Theory]
    [InlineData(null, "Message.")]
    [InlineData("", "Message.")]
    [InlineData("fixture.code", "")]
    public void CreateErrorSanitizesInvalidContracts(string? code, string message)
    {
        var response = ProtocolResponseFactory.CreateError(
            "request-5",
            [new OperationError(message, ErrorType.Validation, code)]);

        var error = Assert.Single(response.Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal(EngineProtocolConstants.UnexpectedFailureErrorCode, error.Code);
        Assert.Equal(EngineProtocolConstants.UnexpectedFailureMessage, error.Message);
    }

    /// <summary>
    ///     Locates the repository root from the compiled test directory.
    /// </summary>
    /// <returns>The absolute path to the ChangeLens repository root.</returns>
    /// <exception cref="DirectoryNotFoundException">The repository root could not be located.</exception>
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
