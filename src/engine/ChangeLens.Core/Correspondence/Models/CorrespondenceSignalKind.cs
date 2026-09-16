namespace ChangeLens.Core.Correspondence.Models;

/// <summary>
///     Defines the signal families that can contribute to a correspondence candidate score.
/// </summary>
public enum CorrespondenceSignalKind
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
    ///     A shared word from a comment.
    /// </summary>
    SharedComment,

    /// <summary>
    ///     A shared repository path component.
    /// </summary>
    PathAffinity,

    /// <summary>
    ///     A boost for shared keys between files written in different recognized languages.
    /// </summary>
    CrossLanguage,

    /// <summary>
    ///     A recency-weighted count of earlier first-parent commits that changed both files.
    /// </summary>
    CoChange,
}
