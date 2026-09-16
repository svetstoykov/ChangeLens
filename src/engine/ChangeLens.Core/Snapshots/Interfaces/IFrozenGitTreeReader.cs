using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Core.Snapshots.Interfaces;

/// <summary>
///     Defines reads against one immutable captured Git snapshot.
/// </summary>
/// <remarks>
///     The session is created for one repository and manifest. It may read only revisions and object identifiers
///     recorded by the manifest or returned by its bounded tree listing; it never reads the worktree.
/// </remarks>
public interface IFrozenGitTreeReader
{
    /// <summary>
    ///     Asynchronously lists readable files from the captured HEAD tree.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the bounded tree listing.</returns>
    Task<Result<FrozenGitTreeListing>> ListTreeAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously reads a captured blob by an identity allowed by the manifest or tree listing.
    /// </summary>
    /// <param name="objectId">The captured blob object identifier. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains text or a binary or size skip reason.</returns>
    Task<Result<FrozenGitBlob>> ReadBlobAsync(string objectId, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously diffs the exact blob identities recorded for one manifest entry.
    /// </summary>
    /// <param name="entry">The captured manifest entry. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains changed lines or a binary or size skip reason.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry" /> is <see langword="null" />.</exception>
    Task<Result<FrozenGitBlobDiff>> ReadBlobDiffAsync(
        SnapshotManifestEntry entry,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously reads the bounded first-parent history reachable from the captured merge-base revision.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the bounded history scan.</returns>
    Task<Result<FrozenGitHistoryScan>> ReadHistoryAsync(CancellationToken cancellationToken);
}
