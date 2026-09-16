namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Defines why an evidence node was quoted.
/// </summary>
public enum EvidenceNodeOrigin
{
    /// <summary>
    ///     The window surrounds changed lines of a captured manifest entry.
    /// </summary>
    ChangedHunk,

    /// <summary>
    ///     The window surrounds a candidate line that holds a shared key.
    /// </summary>
    MatchWindow,

    /// <summary>
    ///     The window is the head of a candidate file that has no quotable match line.
    /// </summary>
    CandidateHead,

    /// <summary>
    ///     The node states a rename or mode change that has no textual hunk to quote.
    /// </summary>
    Manifest,
}
