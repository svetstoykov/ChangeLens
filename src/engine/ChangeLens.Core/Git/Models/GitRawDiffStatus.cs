namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Defines the committed change categories Git's raw diff format can report for one path.
/// </summary>
internal enum GitRawDiffStatus
{
    /// <summary>The path exists only on the HEAD side. Git's raw diff status letter is <c>A</c>.</summary>
    Added,

    /// <summary>The path exists only on the merge-base side. Git's raw diff status letter is <c>D</c>.</summary>
    Deleted,

    /// <summary>
    ///     The path exists on both sides with a different object or mode of the same file type. Git's raw diff status
    ///     letter is <c>M</c>.
    /// </summary>
    Modified,

    /// <summary>
    ///     The path exists on both sides with a different Git file type. Git's raw diff status letter is <c>T</c>.
    /// </summary>
    TypeChanged,

    /// <summary>The path moved from its original path. Git's raw diff status letter is <c>R</c>.</summary>
    Renamed,
}
