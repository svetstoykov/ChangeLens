using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLens.Core.EvidenceBinder.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.EvidenceBinder.Services;

/// <summary>
///     Provides deterministic serialization of the curator-facing binder payload.
/// </summary>
public static class EvidenceBinderJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    ///     Serializes a binder without exposing engine-only match edges, anatomy keys, or policy internals.
    /// </summary>
    /// <param name="binder">The binder to serialize. Cannot be <see langword="null" />.</param>
    /// <returns>The compact curator payload JSON.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="binder" /> is <see langword="null" />.</exception>
    public static string SerializePayload(EvidenceBinderModel binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        var payload = new BinderPayload(
            binder.Comparison,
            binder.DeveloperContext,
            binder.ChangedFiles,
            binder.Evidence,
            binder.Orientation,
            binder.Contract,
            binder.Omissions);
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}
