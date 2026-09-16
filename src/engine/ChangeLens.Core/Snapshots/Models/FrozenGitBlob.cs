namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents text read from one captured Git blob, or a recorded skip.
/// </summary>
/// <param name="ObjectId">The captured blob object identifier. Cannot be <see langword="null" />.</param>
/// <param name="Lines">The decoded lines without a trailing empty line. Cannot be <see langword="null" />.</param>
/// <param name="SkipReason">The reason content was skipped, or <see cref="FrozenGitBlobSkipReason.None" />.</param>
public sealed record FrozenGitBlob(
    string ObjectId,
    IReadOnlyList<string> Lines,
    FrozenGitBlobSkipReason SkipReason)
{
    /// <summary>
    ///     Gets a value indicating whether the blob has readable text.
    /// </summary>
    public bool HasText => this.SkipReason is FrozenGitBlobSkipReason.None;
}
