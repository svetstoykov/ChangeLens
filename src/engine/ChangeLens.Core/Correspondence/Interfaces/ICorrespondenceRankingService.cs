using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeAnatomyModel = ChangeLens.Core.ChangeAnatomy.Models.ChangeAnatomy;

namespace ChangeLens.Core.Correspondence.Interfaces;

/// <summary>
///     Defines ranking of unchanged captured files that share rare text or history with a change.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one analysis request and do not need to be
///     thread-safe.
/// </remarks>
public interface ICorrespondenceRankingService
{
    /// <summary>
    ///     Asynchronously indexes the captured HEAD tree and ranks unchanged files against the change anatomy.
    /// </summary>
    /// <param name="repository">The repository identity accepted for the run. Cannot be <see langword="null" />.</param>
    /// <param name="snapshot">The captured snapshot manifest. Cannot be <see langword="null" />.</param>
    /// <param name="anatomy">The change anatomy extracted from the same snapshot. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while ranking.</param>
    /// <returns>A task whose result contains the capped candidate ranking or a frozen-read failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="repository" />, <paramref name="snapshot" />, or <paramref name="anatomy" /> is
    ///     <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken" /> is canceled.</exception>
    Task<Result<CorrespondenceRanking>> RankAsync(
        AnalysisRepositoryIdentity repository,
        SnapshotManifest snapshot,
        ChangeAnatomyModel anatomy,
        CancellationToken cancellationToken);
}
