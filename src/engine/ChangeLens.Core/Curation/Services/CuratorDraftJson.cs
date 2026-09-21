using System.Text.Json;
using ChangeLens.Core.Curation.Models;

namespace ChangeLens.Core.Curation.Services;

/// <summary>
///     Provides strict parsing for the curator draft JSON contract.
/// </summary>
public static class CuratorDraftJson
{
    private static readonly IReadOnlySet<string> DraftProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "thesis", "tracks", "droppedNodeIds",
    };

    private static readonly IReadOnlySet<string> StatementProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "text", "evidenceNodeIds",
    };

    private static readonly IReadOnlySet<string> TrackProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "id", "title", "summary", "shape", "participants", "relationships", "orderedSteps", "purposes",
    };

    private static readonly IReadOnlySet<string> ParticipantProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "id", "name", "role", "changed", "evidenceNodeIds",
    };

    private static readonly IReadOnlySet<string> RelationshipProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "id", "fromParticipantId", "toParticipantId", "kind", "explanation", "evidenceNodeIds", "matchEdgeIds",
    };

    /// <summary>
    ///     Attempts to parse a complete curator draft without repairing or partially accepting its structure.
    /// </summary>
    /// <param name="json">The raw JSON completion. Cannot be <see langword="null" />.</param>
    /// <param name="draft">The parsed draft, or an empty draft when parsing fails.</param>
    /// <param name="failureReason">The parse failure reason, or <see langword="null" /> on success.</param>
    /// <returns><see langword="true" /> when the complete draft satisfies the JSON shape.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json" /> is <see langword="null" />.</exception>
    public static bool TryParse(string json, out MentalModelDraft draft, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(json);
        draft = EmptyDraft();
        failureReason = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                failureReason = "The draft root must be a JSON object.";
                return false;
            }

            if (!ValidateProperties(document.RootElement, DraftProperties, "MentalModelDraft", out failureReason)
                || !TryGetRequiredObject(document.RootElement, "thesis", "MentalModelDraft", out var thesisElement, out failureReason)
                || !TryParseStatement(thesisElement, "thesis", out var thesis, out failureReason)
                || !TryGetOptionalArray(document.RootElement, "tracks", "MentalModelDraft", out var tracksElement, out failureReason)
                || !TryParseTracks(tracksElement, out var tracks, out failureReason)
                || !TryGetOptionalStringArray(
                    document.RootElement, "droppedNodeIds", "MentalModelDraft", out var droppedNodeIds, out failureReason))
            {
                return false;
            }

            draft = new MentalModelDraft(thesis, tracks, droppedNodeIds);
            return true;
        }
        catch (JsonException exception)
        {
            failureReason = "The draft is not valid JSON: " + exception.Message;
            return false;
        }
        catch (InvalidOperationException exception)
        {
            failureReason = "The draft has an invalid JSON value: " + exception.Message;
            return false;
        }
    }

    /// <summary>
    ///     Creates the empty draft used when a provider reply cannot be parsed.
    /// </summary>
    /// <returns>An empty draft with no statements, tracks, or dropped ids.</returns>
    public static MentalModelDraft EmptyDraft() =>
        new(new BoundStatement(string.Empty, Array.Empty<string>()), Array.Empty<DraftTrack>(), Array.Empty<string>());

    private static bool TryParseTracks(JsonElement? tracksElement, out IReadOnlyList<DraftTrack> tracks, out string? failureReason)
    {
        var parsed = new List<DraftTrack>();
        failureReason = null;
        if (tracksElement is null)
        {
            tracks = parsed;
            return true;
        }

        var index = 0;
        foreach (var element in tracksElement.Value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                tracks = Array.Empty<DraftTrack>();
                failureReason = $"The tracks array element at index {index} must be a JSON object.";
                return false;
            }

            if (!TryParseTrack(element, index, out var track, out failureReason))
            {
                tracks = Array.Empty<DraftTrack>();
                return false;
            }

            parsed.Add(track);
            index++;
        }

        tracks = parsed;
        return true;
    }

    private static bool TryParseTrack(JsonElement element, int index, out DraftTrack track, out string? failureReason)
    {
        track = null!;
        if (!ValidateProperties(element, TrackProperties, $"tracks[{index}]", out failureReason)
            || !TryGetRequiredString(element, "id", $"tracks[{index}]", out var id, out failureReason)
            || !TryGetRequiredString(element, "title", $"tracks[{index}]", out var title, out failureReason)
            || !TryGetRequiredObject(element, "summary", $"tracks[{index}]", out var summaryElement, out failureReason)
            || !TryParseStatement(summaryElement, $"tracks[{index}].summary", out var summary, out failureReason)
            || !TryGetRequiredString(element, "shape", $"tracks[{index}]", out var shape, out failureReason)
            || !TryGetOptionalArray(element, "participants", $"tracks[{index}]", out var participantsElement, out failureReason)
            || !TryParseParticipants(participantsElement, index, out var participants, out failureReason)
            || !TryGetOptionalArray(element, "relationships", $"tracks[{index}]", out var relationshipsElement, out failureReason)
            || !TryParseRelationships(relationshipsElement, index, out var relationships, out failureReason)
            || !TryGetOptionalArray(element, "orderedSteps", $"tracks[{index}]", out var orderedStepsElement, out failureReason)
            || !TryParseStatements(orderedStepsElement, $"tracks[{index}].orderedSteps", out var orderedSteps, out failureReason)
            || !TryGetOptionalArray(element, "purposes", $"tracks[{index}]", out var purposesElement, out failureReason)
            || !TryParseStatements(purposesElement, $"tracks[{index}].purposes", out var purposes, out failureReason))
        {
            return false;
        }

        track = new DraftTrack(id, title, summary, shape, participants, relationships, orderedSteps, purposes);
        return true;
    }

    private static bool TryParseParticipants(
        JsonElement? participantsElement,
        int trackIndex,
        out IReadOnlyList<DraftParticipant> participants,
        out string? failureReason)
    {
        var parsed = new List<DraftParticipant>();
        failureReason = null;
        if (participantsElement is null)
        {
            participants = parsed;
            return true;
        }

        var index = 0;
        foreach (var element in participantsElement.Value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                participants = Array.Empty<DraftParticipant>();
                failureReason = $"The participants array element at tracks[{trackIndex}] index {index} must be a JSON object.";
                return false;
            }

            if (!ValidateProperties(element, ParticipantProperties, $"tracks[{trackIndex}].participants[{index}]", out failureReason)
                || !TryGetRequiredString(element, "id", $"tracks[{trackIndex}].participants[{index}]", out var id, out failureReason)
                || !TryGetRequiredString(element, "name", $"tracks[{trackIndex}].participants[{index}]", out var name, out failureReason)
                || !TryGetRequiredString(element, "role", $"tracks[{trackIndex}].participants[{index}]", out var role, out failureReason)
                || !TryGetRequiredBoolean(element, "changed", $"tracks[{trackIndex}].participants[{index}]", out var changed, out failureReason)
                || !TryGetOptionalStringArray(
                    element, "evidenceNodeIds", $"tracks[{trackIndex}].participants[{index}]", out var evidenceNodeIds, out failureReason))
            {
                participants = Array.Empty<DraftParticipant>();
                return false;
            }

            parsed.Add(new DraftParticipant(id, name, role, changed, evidenceNodeIds));
            index++;
        }

        participants = parsed;
        return true;
    }

    private static bool TryParseRelationships(
        JsonElement? relationshipsElement,
        int trackIndex,
        out IReadOnlyList<DraftRelationship> relationships,
        out string? failureReason)
    {
        var parsed = new List<DraftRelationship>();
        failureReason = null;
        if (relationshipsElement is null)
        {
            relationships = parsed;
            return true;
        }

        var index = 0;
        foreach (var element in relationshipsElement.Value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                relationships = Array.Empty<DraftRelationship>();
                failureReason = $"The relationships array element at tracks[{trackIndex}] index {index} must be a JSON object.";
                return false;
            }

            if (!ValidateProperties(element, RelationshipProperties, $"tracks[{trackIndex}].relationships[{index}]", out failureReason)
                || !TryGetRequiredString(element, "id", $"tracks[{trackIndex}].relationships[{index}]", out var id, out failureReason)
                || !TryGetRequiredString(
                    element, "fromParticipantId", $"tracks[{trackIndex}].relationships[{index}]", out var fromParticipantId, out failureReason)
                || !TryGetRequiredString(
                    element, "toParticipantId", $"tracks[{trackIndex}].relationships[{index}]", out var toParticipantId, out failureReason)
                || !TryGetRequiredString(element, "kind", $"tracks[{trackIndex}].relationships[{index}]", out var kind, out failureReason)
                || !TryGetRequiredString(
                    element, "explanation", $"tracks[{trackIndex}].relationships[{index}]", out var explanation, out failureReason)
                || !TryGetOptionalStringArray(
                    element, "evidenceNodeIds", $"tracks[{trackIndex}].relationships[{index}]", out var evidenceNodeIds, out failureReason)
                || !TryGetOptionalStringArray(
                    element, "matchEdgeIds", $"tracks[{trackIndex}].relationships[{index}]", out var matchEdgeIds, out failureReason))
            {
                relationships = Array.Empty<DraftRelationship>();
                return false;
            }

            parsed.Add(new DraftRelationship(
                id, fromParticipantId, toParticipantId, kind, explanation, evidenceNodeIds, matchEdgeIds));
            index++;
        }

        relationships = parsed;
        return true;
    }

    private static bool TryParseStatements(
        JsonElement? statementsElement,
        string path,
        out IReadOnlyList<BoundStatement> statements,
        out string? failureReason)
    {
        var parsed = new List<BoundStatement>();
        failureReason = null;
        if (statementsElement is null)
        {
            statements = parsed;
            return true;
        }

        var index = 0;
        foreach (var element in statementsElement.Value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !TryParseStatement(element, $"{path}[{index}]", out var statement, out failureReason))
            {
                statements = Array.Empty<BoundStatement>();
                failureReason ??= $"The statement at {path}[{index}] must be a JSON object.";
                return false;
            }

            parsed.Add(statement);
            index++;
        }

        statements = parsed;
        return true;
    }

    private static bool TryParseStatement(JsonElement element, string path, out BoundStatement statement, out string? failureReason)
    {
        statement = null!;
        if (!ValidateProperties(element, StatementProperties, path, out failureReason)
            || !TryGetRequiredString(element, "text", path, out var text, out failureReason)
            || !TryGetOptionalStringArray(element, "evidenceNodeIds", path, out var evidenceNodeIds, out failureReason))
        {
            return false;
        }

        statement = new BoundStatement(text, evidenceNodeIds);
        return true;
    }

    private static bool TryGetRequiredObject(
        JsonElement parent,
        string propertyName,
        string path,
        out JsonElement value,
        out string? failureReason)
    {
        if (!parent.TryGetProperty(propertyName, out value) || value.ValueKind == JsonValueKind.Null)
        {
            failureReason = $"The required object '{path}.{propertyName}' is missing or null.";
            return false;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            failureReason = $"The required object '{path}.{propertyName}' has the wrong JSON type.";
            return false;
        }

        failureReason = null;
        return true;
    }

    private static bool TryGetRequiredString(
        JsonElement parent,
        string propertyName,
        string path,
        out string value,
        out string? failureReason)
    {
        if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            value = string.Empty;
            failureReason = $"The required string '{path}.{propertyName}' is missing or null.";
            return false;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            value = string.Empty;
            failureReason = $"The required string '{path}.{propertyName}' has the wrong JSON type.";
            return false;
        }

        value = element.GetString()!;
        failureReason = null;
        return true;
    }

    private static bool TryGetRequiredBoolean(
        JsonElement parent,
        string propertyName,
        string path,
        out bool value,
        out string? failureReason)
    {
        if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            value = false;
            failureReason = $"The required boolean '{path}.{propertyName}' is missing or null.";
            return false;
        }

        if (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False)
        {
            value = false;
            failureReason = $"The required boolean '{path}.{propertyName}' has the wrong JSON type.";
            return false;
        }

        value = element.GetBoolean();
        failureReason = null;
        return true;
    }

    private static bool TryGetOptionalArray(
        JsonElement parent,
        string propertyName,
        string path,
        out JsonElement? value,
        out string? failureReason)
    {
        if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            value = null;
            failureReason = null;
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            value = null;
            failureReason = $"The array '{path}.{propertyName}' has the wrong JSON type.";
            return false;
        }

        value = element;
        failureReason = null;
        return true;
    }

    private static bool TryGetOptionalStringArray(
        JsonElement parent,
        string propertyName,
        string path,
        out IReadOnlyList<string> values,
        out string? failureReason)
    {
        values = Array.Empty<string>();
        if (!TryGetOptionalArray(parent, propertyName, path, out var element, out failureReason) || element is null)
        {
            return failureReason is null;
        }

        var parsed = new List<string>();
        var index = 0;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                failureReason = $"The array '{path}.{propertyName}' contains a non-string at index {index}.";
                return false;
            }

            parsed.Add(item.GetString()!);
            index++;
        }

        values = parsed;
        failureReason = null;
        return true;
    }

    private static bool ValidateProperties(
        JsonElement element,
        IReadOnlySet<string> allowedProperties,
        string path,
        out string? failureReason)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                failureReason = $"The object '{path}' contains unknown property '{property.Name}'.";
                return false;
            }

            if (!seen.Add(property.Name))
            {
                failureReason = $"The object '{path}' contains duplicate property '{property.Name}'.";
                return false;
            }
        }

        failureReason = null;
        return true;
    }
}
