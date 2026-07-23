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
    [InlineData("engine-status.schema.json", "uncorrelated-error.response.json")]
    [InlineData("error-response.schema.json", "uncorrelated-error.response.json")]
    [InlineData("payload-free-result.schema.json", "engine-check-status.result.json")]
    [InlineData("repository-open.schema.json", "repositories-open.request.json")]
    [InlineData("repository-open.schema.json", "repositories-open.branch.result.json")]
    [InlineData("repository-open.schema.json", "repositories-open.detached.result.json")]
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

    /// <summary>
    ///     Verifies that the repository-open schema rejects malformed requests and results.
    /// </summary>
    /// <param name="json">The malformed repository-open message.</param>
    [Theory]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open"}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{}}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{"path":1}}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{"path":" "}}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{"path":"/repo","extra":true}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"branch","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"detached","name":"main","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"detached","revision":"0123456789ABCDEF0123456789ABCDEF01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"detached","revision":"0123456789abcdef"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":" ","canonicalPath":"/repo","head":{"kind":"branch","name":"main","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":" ","head":{"kind":"branch","name":"main","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    [InlineData("""{"protocolVersion":1,"type":"result","requestId":"id","result":{"repository":{"name":"repo","canonicalPath":"/repo","head":{"kind":"branch","name":" ","revision":"0123456789abcdef0123456789abcdef01234567"}}}}""")]
    public void RepositoryOpenSchemaRejectsMalformedMessages(string json)
    {
        using var instance = JsonDocument.Parse(json);

        var result = Schemas["repository-open.schema.json"].Evaluate(instance.RootElement);

        Assert.False(result.IsValid);
    }

    /// <summary>
    ///     Verifies that duplicate repository-open path properties are rejected before schema evaluation.
    /// </summary>
    [Fact]
    public void RepositoryOpenContractRejectsDuplicatePathProperties()
    {
        const string request =
            """{"protocolVersion":1,"requestId":"id","action":"repositories.open","parameters":{"path":"/first","path":"/second"}}""";

        Assert.False(IsContractValid("repository-open.schema.json", request));
    }

    private static IReadOnlyDictionary<string, JsonSchema> LoadSchemas()
    {
        var names = new[]
        {
            "engine-status.schema.json",
            "error-response.schema.json",
            "payload-free-result.schema.json",
            "repository-open.schema.json",
        };

        return names.ToDictionary(
            name => name,
            name => JsonSchema.FromFile(Path.Combine(RepositoryPaths.EngineProtocolV1, name)),
            StringComparer.Ordinal);
    }

    /// <summary>
    ///     Determines whether raw JSON satisfies a protocol schema after duplicate-property validation.
    /// </summary>
    /// <param name="schemaFileName">The schema used to validate the JSON.</param>
    /// <param name="json">The raw JSON to validate.</param>
    /// <returns>
    ///     <see langword="true" /> if the JSON has no duplicate properties and satisfies the schema; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool IsContractValid(string schemaFileName, string json)
    {
        using var instance = JsonDocument.Parse(json);

        return !ContainsDuplicateProperties(instance.RootElement) &&
               Schemas[schemaFileName].Evaluate(instance.RootElement).IsValid;
    }

    /// <summary>
    ///     Determines whether the JSON value contains an object with duplicate property names.
    /// </summary>
    /// <param name="element">The JSON value to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> if an object contains duplicate property names; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool ContainsDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || ContainsDuplicateProperties(property.Value))
                {
                    return true;
                }
            }

            return false;
        }

        return element.ValueKind == JsonValueKind.Array && element.EnumerateArray().Any(ContainsDuplicateProperties);
    }

    private static string FixturePath(string fileName) => Path.Combine(
        RepositoryPaths.EngineProtocolV1,
        "fixtures",
        fileName);
}
