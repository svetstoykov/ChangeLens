using ChangeLens.Core.ContextPolicy.Models;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.ContextPolicy.Interfaces;

/// <summary>
///     Defines the disclosure decision that allows, redacts, or excludes evidence graph nodes.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one analysis request and do not need to be
///     thread-safe. Disclosure reads only the graph; it performs no repository reads and sends nothing to a model.
/// </remarks>
public interface IContextPolicyService
{
    /// <summary>
    ///     Applies path exclusion, node size limits, and secret redaction to every node, then filters match edges.
    /// </summary>
    /// <param name="graph">The evidence graph to disclose. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe between nodes.</param>
    /// <returns>The per-node and per-edge decisions with the disclosed nodes and edges.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="graph" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken" /> is canceled.</exception>
    ContextPolicyOutcome Apply(EvidenceGraphModel graph, CancellationToken cancellationToken);
}
