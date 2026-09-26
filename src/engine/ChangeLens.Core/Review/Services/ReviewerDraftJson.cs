using System.Text.Json;
using ChangeLens.Core.Review.Models;

namespace ChangeLens.Core.Review.Services;

/// <summary>
///     Provides strict parsing for the complete reviewer draft JSON contract.
/// </summary>
public static class ReviewerDraftJson
{
    private static readonly IReadOnlySet<string> DraftProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "findings",
    };

    private static readonly IReadOnlySet<string> FindingProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "id", "severity", "title", "trigger", "impact", "fix", "evidenceNodeIds", "anchor",
    };

    private static readonly IReadOnlySet<string> AnchorProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "nodeId", "lines",
    };

    /// <summary>
    ///     Attempts to parse a complete reviewer draft without repairing or partially accepting its structure.
    /// </summary>
    /// <param name="json">The raw JSON completion. Cannot be <see langword="null" />.</param>
    /// <param name="draft">The parsed draft, or an empty draft when parsing fails.</param>
    /// <param name="failureReason">The parse failure reason, or <see langword="null" /> on success.</param>
    /// <returns><see langword="true" /> when the complete draft satisfies the JSON shape.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json" /> is <see langword="null" />.</exception>
    public static bool TryParse(string json, out ReviewerDraft draft, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(json);
        draft = EmptyDraft();
        failureReason = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !ValidateProperties(root, DraftProperties))
            {
                failureReason = "The completion must be a JSON object with the required reviewer properties.";
                return false;
            }

            if (!root.TryGetProperty("findings", out var findingsElement) || findingsElement.ValueKind != JsonValueKind.Array)
            {
                failureReason = "The completion must contain a findings array.";
                return false;
            }

            var findings = new List<ReviewerFinding>();
            foreach (var element in findingsElement.EnumerateArray())
            {
                if (!TryParseFinding(element, out var finding))
                {
                    failureReason = "A finding has an invalid or incomplete JSON shape.";
                    return false;
                }

                findings.Add(finding);
            }

            draft = new ReviewerDraft(findings);
            return true;
        }
        catch (JsonException)
        {
            failureReason = "The completion is not valid JSON.";
            return false;
        }
        catch (InvalidOperationException)
        {
            failureReason = "The completion has an invalid JSON value.";
            return false;
        }
    }

    /// <summary>
    ///     Creates the empty draft used when a provider reply cannot be parsed.
    /// </summary>
    /// <returns>An empty draft with no findings.</returns>
    public static ReviewerDraft EmptyDraft() => new(Array.Empty<ReviewerFinding>());

    private static bool TryParseFinding(JsonElement element, out ReviewerFinding finding)
    {
        finding = null!;
        if (element.ValueKind != JsonValueKind.Object
            || !ValidateProperties(element, FindingProperties)
            || !TryGetString(element, "id", out var id)
            || !TryGetString(element, "severity", out var severity)
            || !TryGetString(element, "title", out var title)
            || !TryGetString(element, "trigger", out var trigger)
            || !TryGetString(element, "impact", out var impact)
            || !TryGetString(element, "fix", out var fix)
            || !TryGetStringArray(element, "evidenceNodeIds", out var evidenceNodeIds)
            || !element.TryGetProperty("anchor", out var anchorElement)
            || anchorElement.ValueKind != JsonValueKind.Object
            || !ValidateProperties(anchorElement, AnchorProperties)
            || !TryGetString(anchorElement, "nodeId", out var nodeId)
            || !TryGetString(anchorElement, "lines", out var lines))
        {
            return false;
        }

        finding = new ReviewerFinding(id, severity, title, trigger, impact, fix, evidenceNodeIds, new ReviewerAnchor(nodeId, lines));
        return true;
    }

    private static bool TryGetString(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString()!;
        return true;
    }

    private static bool TryGetStringArray(JsonElement parent, string name, out IReadOnlyList<string> values)
    {
        values = Array.Empty<string>();
        if (!parent.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsed = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            parsed.Add(item.GetString()!);
        }

        values = parsed;
        return true;
    }

    private static bool ValidateProperties(JsonElement element, IReadOnlySet<string> allowedProperties)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name) || !seen.Add(property.Name))
            {
                return false;
            }
        }

        return seen.SetEquals(allowedProperties);
    }
}
