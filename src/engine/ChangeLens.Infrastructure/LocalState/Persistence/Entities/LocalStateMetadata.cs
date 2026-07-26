namespace ChangeLens.Infrastructure.LocalState.Persistence.Entities;

/// <summary>
///     Represents metadata persisted for the local-state database.
/// </summary>
internal sealed class LocalStateMetadata
{
    /// <summary>
    ///     Gets or sets the singleton row identifier.
    /// </summary>
    public int SingletonId { get; set; }

    /// <summary>
    ///     Gets or sets the product name associated with the local-state database.
    /// </summary>
    public required string ProductName { get; set; }

    /// <summary>
    ///     Gets or sets the local-state schema version.
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    ///     Gets or sets the database creation time in UTC Unix milliseconds.
    /// </summary>
    public long CreatedAtUnixMilliseconds { get; set; }
}
