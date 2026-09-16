using System.Security.Cryptography;
using System.Text;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Infrastructure.IntegrationTests.ContextPolicy.Support;

/// <summary>
///     Builds in-memory evidence graphs for context policy tests.
/// </summary>
internal static class ContextPolicyGraphFixtures
{
    private const string ObjectId = "1111111111111111111111111111111111111111";

    /// <summary>
    ///     Computes the <c>sha256:</c>-prefixed lower-case hex hash the evidence graph assigns to quoted text.
    /// </summary>
    /// <param name="text">The quoted text. Cannot be <see langword="null" />.</param>
    /// <returns>The content hash.</returns>
    internal static string Hash(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>
    ///     Creates a quote-window or manifest-fact node with a computed content hash.
    /// </summary>
    /// <param name="nodeId">The stable node id. Cannot be <see langword="null" />.</param>
    /// <param name="path">The repository-relative path. Cannot be <see langword="null" />.</param>
    /// <param name="quotedText">The quoted lines joined with line feeds. Cannot be <see langword="null" />.</param>
    /// <param name="startLine">The first quoted one-based line, or zero for a manifest fact.</param>
    /// <param name="side">The comparison side of the quoted blob.</param>
    /// <param name="isChangedFile">Whether the quoted file is a captured manifest entry.</param>
    /// <returns>The evidence node.</returns>
    internal static EvidenceNode CreateNode(
        string nodeId,
        string path,
        string quotedText,
        int startLine = 1,
        ChangeAnatomySide side = ChangeAnatomySide.After,
        bool isChangedFile = false)
    {
        var lineCount = quotedText.Count(character => character == '\n') + 1;
        var endLine = startLine == 0 ? 0 : startLine + lineCount - 1;
        return new EvidenceNode(
            nodeId, path, side, ObjectId, startLine, endLine, quotedText, Hash(quotedText), isChangedFile,
            [EvidenceNodeOrigin.MatchWindow], 1.0, false);
    }

    /// <summary>
    ///     Creates a match edge between two nodes.
    /// </summary>
    /// <param name="edgeId">The stable edge id. Cannot be <see langword="null" />.</param>
    /// <param name="fromNodeId">The changed-file endpoint node id. Cannot be <see langword="null" />.</param>
    /// <param name="toNodeId">The candidate endpoint node id. Cannot be <see langword="null" />.</param>
    /// <param name="matchedValue">The shared value, or <see langword="null" /> for co-change.</param>
    /// <param name="kind">The correspondence signal family that produced the edge.</param>
    /// <returns>The match edge.</returns>
    internal static MatchEdge CreateEdge(
        string edgeId,
        string fromNodeId,
        string toNodeId,
        string? matchedValue,
        MatchEdgeKind kind = MatchEdgeKind.SharedIdentifier) =>
        new(edgeId, fromNodeId, toNodeId, kind, matchedValue, MatchEdgeAnchor.Quoted, MatchEdgeAnchor.Quoted, null, 1.0);

    /// <summary>
    ///     Creates an evidence graph around the given nodes and edges.
    /// </summary>
    /// <param name="nodes">The graph nodes in graph order. Cannot be <see langword="null" />.</param>
    /// <param name="edges">The graph edges in graph order, or <see langword="null" /> for none.</param>
    /// <returns>The evidence graph.</returns>
    internal static EvidenceGraphModel CreateGraph(IReadOnlyList<EvidenceNode> nodes, IReadOnlyList<MatchEdge>? edges = null)
    {
        var diagnostics = new EvidenceGraphDiagnostics(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0, 0,
            new SortedDictionary<string, int>(StringComparer.Ordinal),
            new SortedDictionary<string, int>(StringComparer.Ordinal),
            new SortedDictionary<string, int>(StringComparer.Ordinal));
        return new EvidenceGraphModel(nodes, edges ?? [], diagnostics);
    }
}
