using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.EvidenceBinder.Models;

namespace ChangeLens.Core.DraftValidation.Helpers;

/// <summary>
///     Provides one undirected graph of the binder's disclosed match edges for movement checks.
/// </summary>
internal sealed class MatchEdgeGraph
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _adjacency;

    private MatchEdgeGraph(IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency) => this._adjacency = adjacency;

    /// <summary>
    ///     Builds a graph from the supplied match edges.
    /// </summary>
    /// <param name="edges">The engine-only match edges disclosed by the binder.</param>
    /// <returns>The undirected graph used by all relationship checks in one validation.</returns>
    internal static MatchEdgeGraph Build(IReadOnlyList<BinderMatchEdge> edges)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!adjacency.TryGetValue(edge.FromNodeId, out var fromNeighbors))
            {
                fromNeighbors = [];
                adjacency.Add(edge.FromNodeId, fromNeighbors);
            }

            if (!adjacency.TryGetValue(edge.ToNodeId, out var toNeighbors))
            {
                toNeighbors = [];
                adjacency.Add(edge.ToNodeId, toNeighbors);
            }

            fromNeighbors.Add(edge.ToNodeId);
            toNeighbors.Add(edge.FromNodeId);
        }

        var readOnly = adjacency.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal);
        return new MatchEdgeGraph(readOnly);
    }

    /// <summary>
    ///     Determines whether a match-edge path joins evidence held by two participants.
    /// </summary>
    /// <param name="from">The participant at the start of the relationship.</param>
    /// <param name="to">The participant at the end of the relationship.</param>
    /// <returns>
    ///     <see langword="true" /> when breadth-first search crosses at least one match edge into the target
    ///     participant's evidence; otherwise, <see langword="false" />.
    /// </returns>
    internal bool HasPath(DraftParticipant from, DraftParticipant to)
    {
        var targets = to.EvidenceNodeIds.ToHashSet(StringComparer.Ordinal);
        var queue = new Queue<string>(from.EvidenceNodeIds.Distinct(StringComparer.Ordinal));
        var seen = from.EvidenceNodeIds.ToHashSet(StringComparer.Ordinal);

        while (queue.TryDequeue(out var current))
        {
            if (!this._adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors)
            {
                if (targets.Contains(neighbor))
                {
                    return true;
                }

                if (seen.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return false;
    }
}
