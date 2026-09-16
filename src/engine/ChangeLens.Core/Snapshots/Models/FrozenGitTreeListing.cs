namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents the bounded readable files found in the captured HEAD tree.
/// </summary>
/// <param name="Files">The files returned in Git tree order. Cannot be <see langword="null" />.</param>
/// <param name="WasTruncated">Whether the configured file cap omitted one or more tree files.</param>
public sealed record FrozenGitTreeListing(
    IReadOnlyList<FrozenGitTreeFile> Files,
    bool WasTruncated);
