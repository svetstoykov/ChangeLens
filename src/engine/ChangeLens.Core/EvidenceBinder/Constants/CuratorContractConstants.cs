namespace ChangeLens.Core.EvidenceBinder.Constants;

/// <summary>
///     Defines the closed vocabulary and identifier format shared by the binder, curator prompt, and draft validator.
/// </summary>
public static class CuratorContractConstants
{
    /// <summary>
    ///     Gets the Walk track shape.
    /// </summary>
    public const string Walk = "Walk";

    /// <summary>
    ///     Gets the ParticipantMap track shape.
    /// </summary>
    public const string ParticipantMap = "ParticipantMap";

    /// <summary>
    ///     Gets the PurposeCards track shape.
    /// </summary>
    public const string PurposeCards = "PurposeCards";

    /// <summary>
    ///     Gets the invokes relationship kind.
    /// </summary>
    public const string Invokes = "invokes";

    /// <summary>
    ///     Gets the returns relationship kind.
    /// </summary>
    public const string Returns = "returns";

    /// <summary>
    ///     Gets the reads relationship kind.
    /// </summary>
    public const string Reads = "reads";

    /// <summary>
    ///     Gets the writes relationship kind.
    /// </summary>
    public const string Writes = "writes";

    /// <summary>
    ///     Gets the publishes relationship kind.
    /// </summary>
    public const string Publishes = "publishes";

    /// <summary>
    ///     Gets the subscribes relationship kind.
    /// </summary>
    public const string Subscribes = "subscribes";

    /// <summary>
    ///     Gets the configures relationship kind.
    /// </summary>
    public const string Configures = "configures";

    /// <summary>
    ///     Gets the supersedes relationship kind.
    /// </summary>
    public const string Supersedes = "supersedes";

    /// <summary>
    ///     Gets the covers relationship kind.
    /// </summary>
    public const string Covers = "covers";

    /// <summary>
    ///     Gets the documents relationship kind.
    /// </summary>
    public const string Documents = "documents";

    /// <summary>
    ///     Gets the contains relationship kind.
    /// </summary>
    public const string Contains = "contains";

    /// <summary>
    ///     Gets the depends-on relationship kind.
    /// </summary>
    public const string DependsOn = "depends-on";

    /// <summary>
    ///     Gets the identifier format described by the curator contract.
    /// </summary>
    public const string IdFormat = "letters, digits, '-', '_' and '.' only; at most 120 characters; unique within its track";

    private static readonly string[] RelationshipKindsStorage =
    [
        Invokes, Returns, Reads, Writes, Publishes, Subscribes, Configures, Supersedes, Covers, Documents, Contains,
        DependsOn,
    ];

    private static readonly string[] TrackShapesStorage = [Walk, ParticipantMap, PurposeCards];

    /// <summary>
    ///     Gets the relationship kinds permitted in a curator draft, in prompt order.
    /// </summary>
    public static IReadOnlyList<string> RelationshipKinds { get; } = Array.AsReadOnly(RelationshipKindsStorage);

    /// <summary>
    ///     Gets the track shapes permitted in a curator draft, in prompt order.
    /// </summary>
    public static IReadOnlyList<string> TrackShapes { get; } = Array.AsReadOnly(TrackShapesStorage);
}
