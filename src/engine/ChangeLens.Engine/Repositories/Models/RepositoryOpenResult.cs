using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents the result of opening a repository.
/// </summary>
/// <param name="RepositoryId">The retained ChangeLens repository identifier.</param>
/// <param name="Repository">The inspected repository identity and HEAD state.</param>
/// <param name="PreferredTarget">The saved full comparison ref, or <see langword="null" />.</param>
internal sealed record RepositoryOpenResult(
    Guid RepositoryId,
    RepositoryResult Repository,
    string? PreferredTarget)
{
    /// <summary>
    ///     Maps a Core repository descriptor to its versioned protocol result.
    /// </summary>
    /// <param name="descriptor">The repository descriptor to map. Cannot be <see langword="null" />.</param>
    /// <returns>The repository-open protocol result.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="descriptor" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     The descriptor contains an unapproved repository HEAD subtype.
    /// </exception>
    internal static RepositoryOpenResult FromOpenedRepository(
        Core.LocalState.Models.OpenedRepository openedRepository)
    {
        ArgumentNullException.ThrowIfNull(openedRepository);

        return new RepositoryOpenResult(
            openedRepository.HistoryEntry.RepositoryId,
            RepositoryResult.FromDescriptor(openedRepository.Repository),
            openedRepository.HistoryEntry.PreferredTargetFullName);
    }
}
