using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Core.Snapshots.Interfaces;

/// <summary>
///     Defines creation of readers scoped to one captured analysis snapshot.
/// </summary>
public interface IFrozenGitTreeReaderFactory
{
    /// <summary>
    ///     Opens a reader for the given captured repository and manifest.
    /// </summary>
    /// <param name="repository">The accepted repository identity. Cannot be <see langword="null" />.</param>
    /// <param name="snapshot">The captured manifest. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing a snapshot-scoped reader, or a validation failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="repository" /> or <paramref name="snapshot" /> is <see langword="null" />.
    /// </exception>
    Result<IFrozenGitTreeReader> Open(
        AnalysisRepositoryIdentity repository,
        SnapshotManifest snapshot);
}
