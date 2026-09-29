using System.Text.Json;
using System.Text.Json.Nodes;
using ChangeLens.Engine.AnalysisRuns.Constants;

namespace ChangeLens.Engine.AnalysisRuns.Helpers;

/// <summary>
///     Upgrades a stored reading model document written before findings existed to the current shape.
/// </summary>
/// <remarks>
///     Only a document that lacks both <c>findings</c> and <c>review</c> is upgraded, to no findings and a review that
///     was not run. A document that carries exactly one of them, or that is not a readable JSON object, is returned
///     unchanged so strict parsing rejects it.
/// </remarks>
internal static class StoredReadingModelUpgrader
{
    private const string FindingsProperty = "findings";
    private const string ReviewProperty = "review";

    /// <summary>Returns the stored reading model JSON in the current shape.</summary>
    /// <param name="json">The stored reading model JSON. Cannot be <see langword="null" />.</param>
    /// <returns>The upgraded JSON, or <paramref name="json" /> when no upgrade applies.</returns>
    internal static string Upgrade(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonObject document;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject parsed || parsed.ContainsKey(FindingsProperty) || parsed.ContainsKey(ReviewProperty))
            {
                return json;
            }

            document = parsed;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return json;
        }

        document[FindingsProperty] = new JsonArray();
        document[ReviewProperty] = new JsonObject
        {
            ["status"] = ReadingModelProtocolConstants.ReviewStatusNotRun,
            ["recommendation"] = null,
            ["withheldCount"] = 0,
        };
        return document.ToJsonString();
    }
}
