namespace ChangeLens.Infrastructure.LocalState.Persistence.Entities;

internal sealed class RepositoryLocalState
{
    public Guid RepositoryId { get; set; }

    public required string CanonicalPath { get; set; }

    public required string CanonicalPathKey { get; set; }

    public required string DisplayName { get; set; }

    public long LastOpenedAtUnixMilliseconds { get; set; }

    public string? PreferredTargetFullName { get; set; }
}
