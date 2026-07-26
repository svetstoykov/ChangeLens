namespace ChangeLens.Infrastructure.LocalState.Persistence.Entities;

internal sealed class LocalStateMetadata
{
    public int SingletonId { get; set; }

    public required string ProductName { get; set; }

    public int SchemaVersion { get; set; }

    public long CreatedAtUnixMilliseconds { get; set; }
}
