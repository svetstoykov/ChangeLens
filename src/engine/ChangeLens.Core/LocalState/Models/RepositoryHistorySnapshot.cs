namespace ChangeLens.Core.LocalState.Models;

/// <summary>
///     Represents the ordered recent repositories and automatic startup selection.
/// </summary>
/// <param name="LastRepositoryId">The last selected repository identifier, or <see langword="null" />.</param>
/// <param name="Repositories">The recent repositories in most-recent-first order.</param>
public sealed record RepositoryHistorySnapshot(
    Guid? LastRepositoryId,
    IReadOnlyList<RepositoryHistoryEntry> Repositories);
