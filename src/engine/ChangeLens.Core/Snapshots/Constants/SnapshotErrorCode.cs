namespace ChangeLens.Core.Snapshots.Constants;

/// <summary>
///     Provides stable errors for frozen snapshot reads.
/// </summary>
public static class SnapshotErrorCode
{
    /// <summary>
    ///     The captured Git object or revision is no longer available.
    /// </summary>
    public const string StaleObject = "snapshot.staleObject";

    /// <summary>
    ///     The requested object is not part of the captured snapshot or returned tree listing.
    /// </summary>
    public const string ObjectNotCaptured = "snapshot.objectNotCaptured";

    /// <summary>
    ///     The captured snapshot arguments are inconsistent or malformed.
    /// </summary>
    public const string InvalidSnapshot = "snapshot.invalid";

    /// <summary>
    ///     Frozen snapshot content could not be inspected through Git.
    /// </summary>
    public const string ReadFailed = "snapshot.readFailed";
}
