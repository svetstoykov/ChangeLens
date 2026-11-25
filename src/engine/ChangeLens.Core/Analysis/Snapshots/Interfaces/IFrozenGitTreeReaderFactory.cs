using ChangeLens.Core.Analysis.Snapshots.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Analysis.Snapshots.Interfaces;

/// <summary>
///     Defines creation of readers scoped to one captured analysis snapshot.
/// </summary>
public interface IFrozenGitTreeReaderFactory
{
    /// <summary>
    ///     Opens a reader for the given repository path and snapshot manifest.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository root path. Cannot be <see langword="null" />.</param>
    /// <param name="snapshot">The captured manifest. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing a snapshot-scoped reader, or a validation failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="canonicalPath" /> or <paramref name="snapshot" /> is <see langword="null" />.
    /// </exception>
    Result<IFrozenGitTreeReader> Open(
        string canonicalPath,
        SnapshotManifest snapshot);
}
