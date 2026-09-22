using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.MentalModels.Models;

namespace ChangeLens.Core.ClaimChecking.Services;

/// <summary>
///     Serializes checker claims and parses the checker's JSON verdict reply.
/// </summary>
public static class CheckerJson
{
    /// <summary>
    ///     The serialized payload with no claims, whose length every fitted payload includes.
    /// </summary>
    public const string EmptyPayload = """{"claims":[]}""";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    ///     Serializes one claim exactly as it appears inside the claims payload.
    /// </summary>
    /// <param name="claim">The claim to serialize. Cannot be <see langword="null" />.</param>
    /// <returns>The compact claim JSON.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="claim" /> is <see langword="null" />.</exception>
    public static string SerializeClaim(CheckerClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return JsonSerializer.Serialize(claim, SerializerOptions);
    }

    /// <summary>
    ///     Serializes the claims payload sent as the checker's user message.
    /// </summary>
    /// <param name="claims">The claims to submit. Cannot be <see langword="null" />.</param>
    /// <returns>The compact payload JSON.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="claims" /> is <see langword="null" />.</exception>
    public static string SerializePayload(IReadOnlyList<CheckerClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        return JsonSerializer.Serialize(new CheckerPayload(claims), SerializerOptions);
    }

    /// <summary>
    ///     Attempts to read verdicts from a checker reply.
    /// </summary>
    /// <remarks>
    ///     The root must be an object with a <c>checks</c> array. An entry is readable when it is an object whose
    ///     <c>claimId</c> is a string, whose <c>verdict</c> is exactly a declared verdict name, whose <c>correctedKind</c>
    ///     is a string or null, and whose <c>focus</c> is null or an array of objects with a string <c>nodeId</c> and
    ///     integer <c>startLine</c> and <c>endLine</c>. Unreadable entries are skipped and counted. Readable verdicts are
    ///     returned in reply order whatever claim id they name. The reply fails when no readable verdict names a
    ///     submitted claim.
    /// </remarks>
    /// <param name="json">The raw completion text. Cannot be <see langword="null" />.</param>
    /// <param name="submittedClaimIds">The claim ids sent in the payload. Cannot be <see langword="null" />.</param>
    /// <param name="verdicts">The readable verdicts, or an empty list when parsing fails.</param>
    /// <param name="unreadableCount">The number of entries skipped because they could not be read.</param>
    /// <param name="failureReason">The parse failure reason, or <see langword="null" /> on success.</param>
    /// <returns><see langword="true" /> when at least one readable verdict names a submitted claim.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    public static bool TryParse(
        string json,
        IReadOnlySet<string> submittedClaimIds,
        out IReadOnlyList<ClaimVerdict> verdicts,
        out int unreadableCount,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(submittedClaimIds);
        verdicts = [];
        unreadableCount = 0;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            failureReason = "The reply is not valid JSON: " + exception.Message;
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("checks", out var checks)
                || checks.ValueKind != JsonValueKind.Array)
            {
                failureReason = "The reply must be a JSON object with a checks array.";
                return false;
            }

            var parsed = new List<ClaimVerdict>();
            foreach (var entry in checks.EnumerateArray())
            {
                if (TryReadVerdict(entry, out var verdict))
                {
                    parsed.Add(verdict);
                }
                else
                {
                    unreadableCount++;
                }
            }

            if (!parsed.Any(verdict => submittedClaimIds.Contains(verdict.ClaimId)))
            {
                failureReason = $"No readable verdict named a submitted claim ({checks.GetArrayLength()} entries returned, "
                    + $"{unreadableCount} unreadable).";
                return false;
            }

            verdicts = parsed;
            failureReason = null;
            return true;
        }
    }

    private static bool TryReadVerdict(JsonElement entry, out ClaimVerdict verdict)
    {
        verdict = null!;
        if (entry.ValueKind != JsonValueKind.Object
            || !TryGetString(entry, "claimId", out var claimId)
            || !TryGetString(entry, "verdict", out var verdictName)
            || !TryReadVerdictKind(verdictName!, out var kind)
            || !TryGetOptionalString(entry, "correctedKind", out var correctedKind)
            || !TryReadFocus(entry, out var focus))
        {
            return false;
        }

        verdict = new ClaimVerdict(claimId!, kind, correctedKind, focus);
        return true;
    }

    private static bool TryReadFocus(JsonElement entry, out IReadOnlyList<FocusRange> focus)
    {
        focus = [];
        if (!entry.TryGetProperty("focus", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var ranges = new List<FocusRange>();
        foreach (var range in element.EnumerateArray())
        {
            if (range.ValueKind != JsonValueKind.Object
                || !TryGetString(range, "nodeId", out var nodeId)
                || !TryGetInt(range, "startLine", out var startLine)
                || !TryGetInt(range, "endLine", out var endLine))
            {
                return false;
            }

            ranges.Add(new FocusRange(nodeId!, startLine, endLine));
        }

        focus = ranges;
        return true;
    }

    private static bool TryReadVerdictKind(string value, out ClaimVerdictKind kind)
    {
        foreach (var candidate in Enum.GetValues<ClaimVerdictKind>())
        {
            if (string.Equals(candidate.ToString(), value, StringComparison.Ordinal))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    private static bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        return value is not null;
    }

    private static bool TryGetOptionalString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryGetInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
    }
}
