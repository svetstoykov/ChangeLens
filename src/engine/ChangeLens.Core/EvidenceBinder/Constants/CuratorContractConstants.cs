namespace ChangeLens.Core.EvidenceBinder.Constants;

/// <summary>
///     Defines the closed vocabulary and identifier format shared by the binder, curator prompt, and draft validator.
/// </summary>
public static class CuratorContractConstants
{
    private static readonly string[] RelationshipKindsStorage =
    [
        "invokes", "returns", "reads", "writes", "publishes", "subscribes", "configures", "supersedes", "covers",
        "documents", "contains", "depends-on",
    ];

    private static readonly string[] TrackShapesStorage = ["Walk", "ParticipantMap", "PurposeCards"];

    /// <summary>
    ///     Gets the relationship kinds permitted in a curator draft, in prompt order.
    /// </summary>
    public static IReadOnlyList<string> RelationshipKinds { get; } = Array.AsReadOnly(RelationshipKindsStorage);

    /// <summary>
    ///     Gets the track shapes permitted in a curator draft, in prompt order.
    /// </summary>
    public static IReadOnlyList<string> TrackShapes { get; } = Array.AsReadOnly(TrackShapesStorage);

    /// <summary>
    ///     Gets the identifier format described by the curator contract.
    /// </summary>
    public const string IdFormat = "letters, digits, '-', '_' and '.' only; at most 120 characters; unique within its track";
}
