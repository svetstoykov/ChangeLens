using System.Text.Json;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Provides exact shape and correlation assertions for real-process protocol responses.
/// </summary>
internal static class ProtocolResponseAssertions
{
    private static readonly string[] AnalysisRunProjectionProperties =
    [
        "runId",
        "state",
        "repository",
        "comparison",
        "checks",
        "requestedAt",
        "captureStartedAt",
        "capturedAt",
        "snapshotId",
        "cancellationRequested",
        "facts",
        "terminal",
        "interruptedAt",
        "interruptionReason",
    ];

    /// <summary>Asserts an exact result envelope and returns its result value.</summary>
    /// <param name="response">The protocol response.</param>
    /// <param name="requestId">The expected request correlation identifier.</param>
    /// <returns>The result value.</returns>
    internal static JsonElement AssertResultEnvelope(JsonDocument response, string requestId)
    {
        var root = response.RootElement;
        AssertExactProperties(root, "protocolVersion", "type", "requestId", "result");
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("result", root.GetProperty("type").GetString());
        Assert.Equal(requestId, root.GetProperty("requestId").GetString());
        return root.GetProperty("result");
    }

    /// <summary>Asserts an exact error envelope and returns its error array.</summary>
    /// <param name="response">The protocol response.</param>
    /// <param name="requestId">The expected request correlation identifier.</param>
    /// <returns>The error array.</returns>
    internal static JsonElement AssertErrorEnvelope(JsonDocument response, string requestId)
    {
        var root = response.RootElement;
        AssertExactProperties(root, "protocolVersion", "type", "requestId", "errors");
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("error", root.GetProperty("type").GetString());
        Assert.Equal(requestId, root.GetProperty("requestId").GetString());
        return root.GetProperty("errors");
    }

    /// <summary>Asserts the exact gate 2.1 analysis-run projection shape.</summary>
    /// <param name="projection">The analysis-run projection.</param>
    internal static void AssertAnalysisRunProjection(JsonElement projection)
    {
        AssertExactProperties(projection, AnalysisRunProjectionProperties);
        AssertExactProperties(projection.GetProperty("repository"), "repositoryId", "displayName", "canonicalPath", "head");
        AssertExactProperties(projection.GetProperty("comparison"), "target", "targetRevision", "freshnessToken");
        AssertExactProperties(projection.GetProperty("checks"), "build", "tests");
        Assert.Equal(JsonValueKind.Array, projection.GetProperty("facts").ValueKind);
    }

    /// <summary>Asserts an object contains exactly the expected property names.</summary>
    /// <param name="element">The JSON object.</param>
    /// <param name="expectedNames">The exact expected property names.</param>
    internal static void AssertExactProperties(JsonElement element, params string[] expectedNames)
    {
        var actualNames = element.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedNames.Order(StringComparer.Ordinal), actualNames);
    }
}
