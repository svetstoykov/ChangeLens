using ChangeLens.Core.Analysis.ContextPolicy.Models;
using ChangeLens.Core.Analysis.Correspondence.Models;
using ChangeLens.Core.Analysis.EvidenceFrontier.Models;
using EvidenceBinderModel = ChangeLens.Core.Analysis.EvidenceBinder.Models.EvidenceBinder;
using EvidenceFrontierModel = ChangeLens.Core.Analysis.EvidenceFrontier.Models.EvidenceFrontier;
using EvidenceGraphModel = ChangeLens.Core.Analysis.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.Analysis.EvidenceFrontier.Interfaces;

/// <summary>
///     Defines construction of the bounded evidence omitted from a published reading model.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They inspect supplied analysis results only and do not read
///     repository files or call a model provider.
/// </remarks>
public interface IEvidenceFrontierService
{
    /// <summary>
    ///     Builds the evidence frontier from ranking, graph, policy, and binder results.
    /// </summary>
    /// <param name="ranking">The correspondence ranking. Cannot be <see langword="null" />.</param>
    /// <param name="graph">The complete evidence graph. Cannot be <see langword="null" />.</param>
    /// <param name="policy">The context-policy outcome. Cannot be <see langword="null" />.</param>
    /// <param name="binder">The evidence binder outcome. Cannot be <see langword="null" />.</param>
    /// <param name="usedNodeIds">
    ///     The node ids cited by a checked model, or <see langword="null" /> when checking did not run.
    /// </param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while building.</param>
    /// <returns>The bounded frontier and its pre-cap diagnostics.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="ranking" />, <paramref name="graph" />, <paramref name="policy" />, or
    ///     <paramref name="binder" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured maximum entry count is not positive.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken" /> is canceled.</exception>
    EvidenceFrontierModel Build(
        CorrespondenceRanking ranking,
        EvidenceGraphModel graph,
        ContextPolicyOutcome policy,
        EvidenceBinderModel binder,
        IReadOnlySet<string>? usedNodeIds,
        CancellationToken cancellationToken);
}
