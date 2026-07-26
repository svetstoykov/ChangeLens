namespace ChangeLens.Infrastructure.LocalState.Persistence.Entities;

/// <summary>
///     Represents persisted local state for one repository.
/// </summary>
internal sealed class RepositoryLocalState
{
    /// <summary>
    ///     Gets or sets the ChangeLens-generated repository identifier.
    /// </summary>
    public Guid RepositoryId { get; set; }

    /// <summary>
    ///     Gets or sets the canonical absolute repository path.
    /// </summary>
    public required string CanonicalPath { get; set; }

    /// <summary>
    ///     Gets or sets the normalized key derived from the canonical repository path.
    /// </summary>
    public required string CanonicalPathKey { get; set; }

    /// <summary>
    ///     Gets or sets the repository display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    ///     Gets or sets the last successful open time in UTC Unix milliseconds.
    /// </summary>
    public long LastOpenedAtUnixMilliseconds { get; set; }

    /// <summary>
    ///     Gets or sets the saved full comparison reference, or <see langword="null" /> when none is configured.
    /// </summary>
    public string? PreferredTargetFullName { get; set; }
}
