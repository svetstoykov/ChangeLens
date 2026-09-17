using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ChangeLens.Core.ChangeAnatomy.Services;
using ChangeLens.Core.ContextPolicy.Interfaces;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using Microsoft.Extensions.Logging;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.ContextPolicy.Services;

/// <summary>
///     Applies path exclusion, node size limits, and secret redaction to an evidence graph.
/// </summary>
/// <remarks>
///     <para>
///         The Engine registers this service as scoped. It serves one analysis request and does not need to be
///         thread-safe.
///     </para>
///     <para>
///         Disclosure reads only the in-memory graph. It performs no repository reads and sends nothing to a model.
///     </para>
/// </remarks>
/// <param name="graphOptions">The evidence graph bounds that cap quoted lines. Cannot be <see langword="null" />.</param>
/// <param name="options">The disclosure bounds. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for disclosure counts. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="graphOptions" />, <paramref name="options" />, or <paramref name="logger" /> is
///     <see langword="null" />.
/// </exception>
public sealed class ContextPolicyService(
    EvidenceGraphOptions graphOptions,
    ContextPolicyOptions options,
    ILogger<ContextPolicyService> logger) : IContextPolicyService
{
    private readonly EvidenceGraphOptions _graphOptions = graphOptions ?? throw new ArgumentNullException(nameof(graphOptions));
    private readonly ContextPolicyOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<ContextPolicyService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public ContextPolicyOutcome Apply(EvidenceGraphModel graph, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ValidateOptions(this._graphOptions, this._options);
        cancellationToken.ThrowIfCancellationRequested();
        var decisions = new List<ContextPolicyDecision>(graph.Nodes.Count);
        var disclosedNodes = new List<DisclosedEvidenceNode>(graph.Nodes.Count);
        var disclosedTextByNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
        var excludedByReason = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var redactionsByPattern = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var allowedNodeCount = 0;
        var redactedNodeCount = 0;
        var excludedNodeCount = 0;
        var redactedLineCount = 0;
        var disclosedCharacterCount = 0;
        foreach (var node in graph.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathReason = ChangeAnatomyPathRules.ExclusionReason(node.Path);
            if (pathReason is not null)
            {
                this.Exclude(decisions, excludedByReason, node, $"path excluded: {pathReason}");
                excludedNodeCount++;
                continue;
            }

            var lineCount = node.QuotedText.Count(character => character == '\n') + 1;
            if (lineCount > this._graphOptions.MaximumQuotedLinesPerNode)
            {
                var maximumLines = string.Create(CultureInfo.InvariantCulture, $"{this._graphOptions.MaximumQuotedLinesPerNode}");
                this.Exclude(decisions, excludedByReason, node, $"more than {maximumLines} quoted lines");
                excludedNodeCount++;
                continue;
            }

            if (node.QuotedText.Length > this._options.MaximumDisclosedCharactersPerNode)
            {
                var maximumCharacters = string.Create(CultureInfo.InvariantCulture, $"{this._options.MaximumDisclosedCharactersPerNode}");
                this.Exclude(decisions, excludedByReason, node, $"more than {maximumCharacters} characters");
                excludedNodeCount++;
                continue;
            }

            var lines = node.QuotedText.Split('\n');
            var matches = ContextPolicySecretScanner.Scan(lines, this._options.MinimumSecretValueLength);
            if (matches.Count == 0)
            {
                this.Allow(decisions, disclosedNodes, disclosedTextByNodeId, node);
                allowedNodeCount++;
                disclosedCharacterCount += node.QuotedText.Length;
                continue;
            }

            if (matches.Count > lines.Length * this._options.MaximumRedactedLineShare)
            {
                var share = string.Create(CultureInfo.InvariantCulture, $"{this._options.MaximumRedactedLineShare:0.##}");
                this.Exclude(decisions, excludedByReason, node, $"redaction covers more than {share} of the node's lines");
                excludedNodeCount++;
                continue;
            }

            var disclosedText = this.Redact(decisions, disclosedNodes, disclosedTextByNodeId, redactionsByPattern, node, lines, matches);
            redactedNodeCount += 1;
            redactedLineCount += matches.Count;
            disclosedCharacterCount += disclosedText.Length;
        }

        var edgeDecisions = new List<ContextPolicyEdgeDecision>(graph.Edges.Count);
        var disclosedEdges = new List<DisclosedMatchEdge>(graph.Edges.Count);
        var droppedEdgeCount = 0;
        var withheldMatchedValueCount = 0;
        foreach (var edge in graph.Edges)
        {
            if (!disclosedTextByNodeId.TryGetValue(edge.FromNodeId, out var fromText)
                || !disclosedTextByNodeId.TryGetValue(edge.ToNodeId, out var toText))
            {
                edgeDecisions.Add(new ContextPolicyEdgeDecision(edge.EdgeId, false, false));
                droppedEdgeCount++;
                continue;
            }

            var withheld = edge.MatchedValue is not null
                && !fromText.Contains(edge.MatchedValue, StringComparison.OrdinalIgnoreCase)
                && !toText.Contains(edge.MatchedValue, StringComparison.OrdinalIgnoreCase);
            disclosedEdges.Add(new DisclosedMatchEdge(
                edge.EdgeId, edge.FromNodeId, edge.ToNodeId, edge.Kind, withheld ? null : edge.MatchedValue,
                edge.FromAnchor, edge.ToAnchor, edge.Contribution));
            edgeDecisions.Add(new ContextPolicyEdgeDecision(edge.EdgeId, true, withheld));
            if (withheld)
            {
                withheldMatchedValueCount++;
            }
        }

        var diagnostics = new ContextPolicyDiagnostics(
            graph.Nodes.Count, allowedNodeCount, redactedNodeCount, excludedNodeCount, redactedLineCount,
            disclosedCharacterCount, graph.Edges.Count, disclosedEdges.Count, droppedEdgeCount, withheldMatchedValueCount,
            excludedByReason, redactionsByPattern);
        this._logger.LogInformation(
            "Context policy completed with {NodeCount} node(s): {AllowedNodeCount} allowed, {RedactedNodeCount} redacted, " +
            "{ExcludedNodeCount} excluded, {RedactedLineCount} line(s) redacted, {DisclosedEdgeCount} edge(s) disclosed, " +
            "{WithheldMatchedValueCount} matched value(s) withheld.",
            graph.Nodes.Count, allowedNodeCount, redactedNodeCount, excludedNodeCount, redactedLineCount,
            disclosedEdges.Count, withheldMatchedValueCount);
        foreach (var entry in excludedByReason)
        {
            this._logger.LogDebug(
                "Context policy excluded {ExcludedNodeCount} node(s) by reason {ExclusionReason}.", entry.Value, entry.Key);
        }

        foreach (var entry in redactionsByPattern)
        {
            this._logger.LogDebug(
                "Context policy redacted {RedactedLineCount} line(s) with pattern {PatternName}.", entry.Value, entry.Key);
        }

        return new ContextPolicyOutcome(decisions, edgeDecisions, disclosedNodes, disclosedEdges, diagnostics);
    }

    private void Allow(
        ICollection<ContextPolicyDecision> decisions,
        ICollection<DisclosedEvidenceNode> disclosedNodes,
        IDictionary<string, string> disclosedTextByNodeId,
        EvidenceNode node)
    {
        disclosedNodes.Add(new DisclosedEvidenceNode(
            node.NodeId, node.Path, node.Side, node.ObjectId, node.StartLine, node.EndLine, node.IsChangedFile,
            node.Origins, node.Salience, node.QuotedText, node.ContentHash, null, node.Truncated));
        decisions.Add(new ContextPolicyDecision(node.NodeId, node.Path, ContextPolicyVerdict.Allow, null, []));
        disclosedTextByNodeId[node.NodeId] = node.QuotedText;
    }

    private string Redact(
        ICollection<ContextPolicyDecision> decisions,
        ICollection<DisclosedEvidenceNode> disclosedNodes,
        IDictionary<string, string> disclosedTextByNodeId,
        IDictionary<string, int> redactionsByPattern,
        EvidenceNode node,
        string[] lines,
        IReadOnlyList<ContextPolicySecretMatch> matches)
    {
        var redactions = new List<ContextPolicyRedaction>(matches.Count);
        foreach (var match in matches)
        {
            lines[match.LineIndex] = $"«redacted: {match.PatternName}»";
            var lineNumber = node.StartLine == 0 ? match.LineIndex + 1 : node.StartLine + match.LineIndex;
            redactions.Add(new ContextPolicyRedaction(lineNumber, match.PatternName));
            redactionsByPattern[match.PatternName] = redactionsByPattern.TryGetValue(match.PatternName, out var existing) ? existing + 1 : 1;
        }

        var text = string.Join('\n', lines);
        disclosedNodes.Add(new DisclosedEvidenceNode(
            node.NodeId, node.Path, node.Side, node.ObjectId, node.StartLine, node.EndLine, node.IsChangedFile,
            node.Origins, node.Salience, text, node.ContentHash, Hash(text), node.Truncated));
        decisions.Add(new ContextPolicyDecision(node.NodeId, node.Path, ContextPolicyVerdict.Redact, null, redactions));
        disclosedTextByNodeId[node.NodeId] = text;
        return text;
    }

    private void Exclude(
        ICollection<ContextPolicyDecision> decisions,
        IDictionary<string, int> excludedByReason,
        EvidenceNode node,
        string reason)
    {
        decisions.Add(new ContextPolicyDecision(node.NodeId, node.Path, ContextPolicyVerdict.Exclude, reason, []));
        excludedByReason[reason] = excludedByReason.TryGetValue(reason, out var existing) ? existing + 1 : 1;
    }

    private static string Hash(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static void ValidateOptions(EvidenceGraphOptions graphOptions, ContextPolicyOptions options)
    {
        if (graphOptions.MaximumQuotedLinesPerNode <= 0
            || options.MaximumDisclosedCharactersPerNode <= 0
            || options.MinimumSecretValueLength <= 0
            || options.MaximumRedactedLineShare <= 0
            || options.MaximumRedactedLineShare > 1)
        {
            throw new InvalidOperationException("Context policy options must contain valid bounds.");
        }
    }
}
