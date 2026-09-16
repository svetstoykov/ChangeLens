using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeAnatomyModel = ChangeLens.Core.ChangeAnatomy.Models.ChangeAnatomy;

namespace ChangeLens.Core.ChangeAnatomy.Interfaces;

/// <summary>
///     Defines deterministic key extraction from one frozen change snapshot.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one analysis request and do not need to be
///     thread-safe.
/// </remarks>
public interface IChangeAnatomyService
{
    /// <summary>
    ///     Asynchronously extracts keys from changed lines in the captured manifest.
    /// </summary>
    /// <param name="repository">The repository identity accepted for the run. Cannot be <see langword="null" />.</param>
    /// <param name="snapshot">The captured snapshot manifest. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while extracting keys.</param>
    /// <returns>A task whose result contains the per-file anatomy or a frozen-read failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="repository" /> or <paramref name="snapshot" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken" /> is canceled.</exception>
    Task<Result<ChangeAnatomyModel>> AnalyzeAsync(
        AnalysisRepositoryIdentity repository,
        SnapshotManifest snapshot,
        CancellationToken cancellationToken);
}
