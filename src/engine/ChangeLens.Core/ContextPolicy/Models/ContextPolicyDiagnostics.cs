namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Represents aggregate disclosure counts for one evidence graph.
/// </summary>
/// <param name="NodeCount">The number of graph nodes inspected.</param>
/// <param name="AllowedNodeCount">The number of nodes disclosed unchanged.</param>
/// <param name="RedactedNodeCount">The number of nodes disclosed with redacted lines.</param>
/// <param name="ExcludedNodeCount">The number of nodes not disclosed.</param>
/// <param name="RedactedLineCount">The number of lines replaced across redacted nodes.</param>
/// <param name="DisclosedCharacterCount">The number of UTF-16 characters across disclosed node text.</param>
/// <param name="EdgeCount">The number of graph edges inspected.</param>
/// <param name="DisclosedEdgeCount">The number of edges disclosed.</param>
/// <param name="DroppedEdgeCount">The number of edges dropped because an endpoint was excluded.</param>
/// <param name="WithheldMatchedValueCount">The number of disclosed edges that withhold their matched value.</param>
/// <param name="ExcludedByReason">The excluded node counts by short reason. Cannot be <see langword="null" />.</param>
/// <param name="RedactionsByPattern">The redacted line counts by secret pattern name. Cannot be <see langword="null" />.</param>
public sealed record ContextPolicyDiagnostics(
    int NodeCount,
    int AllowedNodeCount,
    int RedactedNodeCount,
    int ExcludedNodeCount,
    int RedactedLineCount,
    int DisclosedCharacterCount,
    int EdgeCount,
    int DisclosedEdgeCount,
    int DroppedEdgeCount,
    int WithheldMatchedValueCount,
    IReadOnlyDictionary<string, int> ExcludedByReason,
    IReadOnlyDictionary<string, int> RedactionsByPattern);
