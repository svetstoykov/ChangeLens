using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeAnatomyModel = ChangeLens.Core.ChangeAnatomy.Models.ChangeAnatomy;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.EvidenceGraph.Interfaces;

/// <summary>
///     Defines construction of the bounded quote graph for one captured change.
/// </summary>
/// <remarks>
///     <para>
///         Implementations are registered as scoped services. They serve one analysis request and do not need to be
///         thread-safe.
///     </para>
///     <para>
///         The graph quotes frozen blobs only. It does not disclose, redact, or budget characters; context policy does.
///     </para>
/// </remarks>
public interface IEvidenceGraphService
{
    /// <summary>
    ///     Asynchronously quotes changed hunks, manifest facts, and candidate match lines, and attaches match edges.
    /// </summary>
    /// <param name="repository">The repository identity accepted for the run. Cannot be <see langword="null" />.</param>
    /// <param name="snapshot">The captured snapshot manifest. Cannot be <see langword="null" />.</param>
    /// <param name="anatomy">The change anatomy extracted from the same snapshot. Cannot be <see langword="null" />.</param>
    /// <param name="ranking">The correspondence ranking for the same anatomy. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while building.</param>
    /// <returns>A task whose result contains the bounded evidence graph or a frozen-read failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="repository" />, <paramref name="snapshot" />, <paramref name="anatomy" />, or
    ///     <paramref name="ranking" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken" /> is canceled.</exception>
    Task<Result<EvidenceGraphModel>> BuildAsync(
        AnalysisRepositoryIdentity repository,
        SnapshotManifest snapshot,
        ChangeAnatomyModel anatomy,
        CorrespondenceRanking ranking,
        CancellationToken cancellationToken);
}
