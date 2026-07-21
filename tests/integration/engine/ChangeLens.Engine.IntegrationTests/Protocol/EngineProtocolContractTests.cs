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
    [InlineData("engine-information.schema.json", "engine-get-info.request.json")]
    [InlineData("engine-information.schema.json", "engine-get-info.result.json")]
    [InlineData("engine-information.schema.json", "ordered-errors.response.json")]
    [InlineData("error-response.schema.json", "ordered-errors.response.json")]
    [InlineData("payload-free-result.schema.json", "payload-free.result.json")]
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

    private static IReadOnlyDictionary<string, JsonSchema> LoadSchemas()
    {
        var names = new[]
        {
            "error-response.schema.json",
            "payload-free-result.schema.json",
            "engine-information.schema.json",
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
