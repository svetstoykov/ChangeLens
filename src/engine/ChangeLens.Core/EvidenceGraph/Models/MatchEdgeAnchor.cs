namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Defines where a match edge's value sits relative to the node an endpoint points at.
/// </summary>
public enum MatchEdgeAnchor
{
    /// <summary>
    ///     The node quotes a line that holds the matched value.
    /// </summary>
    Quoted,

    /// <summary>
    ///     The matched value comes from the file path, not from a line.
    /// </summary>
    PathOnly,

    /// <summary>
    ///     The value is on a line no selected node quotes, or the edge has no value; the node stands for its file.
    /// </summary>
    FileOnly,
}
