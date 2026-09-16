namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents one readable blob returned by a captured tree listing.
/// </summary>
/// <param name="Path">The repository-relative path. Cannot be <see langword="null" />.</param>
/// <param name="ObjectId">The captured blob object identifier. Cannot be <see langword="null" />.</param>
/// <param name="SizeInBytes">The committed blob size in bytes.</param>
/// <param name="Mode">The raw Git tree-entry mode. Cannot be <see langword="null" />.</param>
public sealed record FrozenGitTreeFile(
    string Path,
    string ObjectId,
    long SizeInBytes,
    string Mode);
