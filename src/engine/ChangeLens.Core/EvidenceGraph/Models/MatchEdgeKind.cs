namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Defines the correspondence signal families that can produce a match edge.
/// </summary>
public enum MatchEdgeKind
{
    /// <summary>
    ///     A shared identifier or identifier part.
    /// </summary>
    SharedIdentifier,

    /// <summary>
    ///     A shared string literal or literal segment.
    /// </summary>
    SharedLiteral,

    /// <summary>
    ///     A shared comment word.
    /// </summary>
    SharedComment,

    /// <summary>
    ///     A shared path stem.
    /// </summary>
    PathAffinity,

    /// <summary>
    ///     Recent first-parent commits changed both files.
    /// </summary>
    CoChange,
}
