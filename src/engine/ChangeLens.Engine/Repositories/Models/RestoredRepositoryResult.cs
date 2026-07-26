namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents a successfully revalidated startup repository.
/// </summary>
/// <param name="RepositoryId">The retained ChangeLens repository identifier.</param>
/// <param name="Repository">The current inspected repository facts.</param>
/// <param name="PreferredTarget">The saved full comparison ref, or <see langword="null" />.</param>
internal sealed record RestoredRepositoryResult(
    Guid RepositoryId,
    RepositoryResult Repository,
    string? PreferredTarget) : RepositoryRestoreResult;
