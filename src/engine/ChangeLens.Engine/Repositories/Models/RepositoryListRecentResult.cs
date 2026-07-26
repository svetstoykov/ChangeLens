using ChangeLens.Core.LocalState.Models;

namespace ChangeLens.Engine.Repositories.Models;

/// <summary>
///     Represents recent repository metadata in the protocol.
/// </summary>
/// <param name="LastRepositoryId">The automatic startup selection, or <see langword="null" />.</param>
/// <param name="Repositories">The ordered recent repositories.</param>
internal sealed record RepositoryListRecentResult(
    Guid? LastRepositoryId,
    IReadOnlyList<RecentRepositoryResult> Repositories)
{
    /// <summary>
    ///     Maps a Core repository-history snapshot to its protocol result.
    /// </summary>
    /// <param name="snapshot">The history snapshot.</param>
    /// <returns>The protocol history result.</returns>
    internal static RepositoryListRecentResult FromSnapshot(RepositoryHistorySnapshot snapshot) =>
        new(
            snapshot.LastRepositoryId,
            snapshot.Repositories.Select(
                entry => new RecentRepositoryResult(
                    entry.RepositoryId,
                    entry.DisplayName,
                    entry.CanonicalPath,
                    entry.LastOpenedAtUnixMilliseconds,
                    entry.PreferredTargetFullName)).ToArray());
}
