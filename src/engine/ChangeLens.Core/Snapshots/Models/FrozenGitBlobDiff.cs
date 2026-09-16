namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents changed lines between the exact blobs captured for one manifest entry.
/// </summary>
/// <param name="AddedLines">The added lines on the HEAD side. Cannot be <see langword="null" />.</param>
/// <param name="RemovedLines">The removed lines on the merge-base side. Cannot be <see langword="null" />.</param>
/// <param name="SkipReason">The reason content was skipped, or <see cref="FrozenGitBlobSkipReason.None" />.</param>
public sealed record FrozenGitBlobDiff(
    IReadOnlyList<FrozenGitDiffLine> AddedLines,
    IReadOnlyList<FrozenGitDiffLine> RemovedLines,
    FrozenGitBlobSkipReason SkipReason)
{
    /// <summary>
    ///     Gets a value indicating whether the diff contains readable text.
    /// </summary>
    public bool HasContent => this.SkipReason is FrozenGitBlobSkipReason.None;
}
