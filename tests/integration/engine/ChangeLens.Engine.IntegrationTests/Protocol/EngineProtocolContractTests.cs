using System.Text.Json;
using ChangeLens.Engine.IntegrationTests.Support;
using Json.Schema;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies the version 1 engine-protocol schemas and shared fixtures.
/// </summary>
public sealed class EngineProtocolContractTests
{
    private static readonly IReadOnlyDictionary<string, JsonSchema> Schemas = LoadSchemas();

    /// <summary>
    ///     Verifies that a shared fixture satisfies its declared protocol schema.
    /// </summary>
    /// <param name="schemaFileName">The schema file used to validate the fixture.</param>
    /// <param name="fixtureFileName">The fixture file relative to the v1 fixtures directory.</param>
    [Theory]
    [InlineData("engine-status.schema.json", "engine-check-status.request.json")]
    [InlineData("engine-status.schema.json", "engine-check-status.result.json")]
    [InlineData("engine-status.schema.json", "ordered-errors.response.json")]
    [InlineData("error-response.schema.json", "ordered-errors.response.json")]
    [InlineData("payload-free-result.schema.json", "engine-check-status.result.json")]
    public void SharedFixtureMatchesSchema(string schemaFileName, string fixtureFileName)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(FixturePath(fixtureFileName)));

        var result = Schemas[schemaFileName].Evaluate(fixture.RootElement);

        Assert.True(result.IsValid);
    }

    /// <summary>
    ///     Verifies that the shared error schema rejects empty and malformed error collections.
    /// </summary>
    /// <param name="json">The invalid error response.</param>
    [Theory]
    [InlineData("""{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[]}""")]
    [InlineData("""{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[{"type":"Validation","code":"","message":"Bad"}]}""")]
    [InlineData("""{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[{"type":"Unknown","code":"fixture.first","message":"Bad"}]}""")]
    public void ErrorSchemaRejectsInvalidCollections(string json)
    {
        using var instance = JsonDocument.Parse(json);

        var result = Schemas["error-response.schema.json"].Evaluate(instance.RootElement);

        Assert.False(result.IsValid);
    }

    /// <summary>
    ///     Verifies that protocol schemas reject identifiers and error text containing only whitespace.
    /// </summary>
    /// <param name="schemaFileName">The schema used to validate the document.</param>
    /// <param name="json">The document containing a whitespace-only string.</param>
    [Theory]
    [InlineData(
        "engine-status.schema.json",
        """{"protocolVersion":1,"requestId":" \t ","action":"engine.checkStatus"}""")]
    [InlineData(
        "payload-free-result.schema.json",
        """{"protocolVersion":1,"type":"result","requestId":" \t ","result":null}""")]
    [InlineData(
        "error-response.schema.json",
        """{"protocolVersion":1,"type":"error","requestId":" \t ","errors":[{"type":"Validation","code":"fixture.first","message":"Bad"}]}""")]
    [InlineData(
        "error-response.schema.json",
        """{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[{"type":"Validation","code":" \t ","message":"Bad"}]}""")]
    [InlineData(
        "error-response.schema.json",
        """{"protocolVersion":1,"type":"error","requestId":"desktop-43","errors":[{"type":"Validation","code":"fixture.first","message":" \t "}]}""")]
    public void SchemasRejectWhitespaceOnlyStrings(string schemaFileName, string json)
    {
        using var instance = JsonDocument.Parse(json);

        var result = Schemas[schemaFileName].Evaluate(instance.RootElement);

        Assert.False(result.IsValid);
    }

    private static IReadOnlyDictionary<string, JsonSchema> LoadSchemas()
    {
        var names = new[]
        {
            "engine-status.schema.json",
            "error-response.schema.json",
            "payload-free-result.schema.json",
        };

        return names.ToDictionary(
            name => name,
            name => JsonSchema.FromFile(Path.Combine(RepositoryPaths.EngineProtocolV1, name)),
            StringComparer.Ordinal);
    }

    private static string FixturePath(string fileName) => Path.Combine(
        RepositoryPaths.EngineProtocolV1,
        "fixtures",
        fileName);
}
