using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;

/// <summary>
///     Provides snapshot manifest entries built from the real blob identities in a temporary Git repository.
/// </summary>
internal static class SnapshotManifestEntryFixtures
{
    /// <summary>
    ///     Creates an added-file entry for a path that exists only at the head revision.
    /// </summary>
    /// <param name="repository">The fixture repository. Cannot be <see langword="null" />.</param>
    /// <param name="head">The head revision. Cannot be <see langword="null" />.</param>
    /// <param name="path">The repository-relative path. Cannot be <see langword="null" />.</param>
    /// <returns>The added manifest entry.</returns>
    internal static SnapshotManifestEntry CreateAddedEntry(TemporaryGitRepository repository, string head, string path)
    {
        var objectId = ResolveBlob(repository, head, path);
        var absentObjectId = new string('0', objectId.Length);
        return new SnapshotManifestEntry(path, null, SnapshotChangeCategory.Added, new string('0', 6), "100644", absentObjectId, objectId);
    }

    /// <summary>
    ///     Creates a deleted-file entry for a path that exists only at the merge-base revision.
    /// </summary>
    /// <param name="repository">The fixture repository. Cannot be <see langword="null" />.</param>
    /// <param name="mergeBase">The merge-base revision. Cannot be <see langword="null" />.</param>
    /// <param name="path">The repository-relative path. Cannot be <see langword="null" />.</param>
    /// <returns>The deleted manifest entry.</returns>
    internal static SnapshotManifestEntry CreateDeletedEntry(TemporaryGitRepository repository, string mergeBase, string path)
    {
        var objectId = ResolveBlob(repository, mergeBase, path);
        var absentObjectId = new string('0', objectId.Length);
        return new SnapshotManifestEntry(path, null, SnapshotChangeCategory.Deleted, "100644", new string('0', 6), objectId, absentObjectId);
    }

    /// <summary>
    ///     Creates an entry for a path that exists on both sides, such as a modification or rename.
    /// </summary>
    /// <param name="repository">The fixture repository. Cannot be <see langword="null" />.</param>
    /// <param name="mergeBase">The merge-base revision. Cannot be <see langword="null" />.</param>
    /// <param name="head">The head revision. Cannot be <see langword="null" />.</param>
    /// <param name="mergeBasePath">The path at the merge-base revision. Cannot be <see langword="null" />.</param>
    /// <param name="headPath">The path at the head revision. Cannot be <see langword="null" />.</param>
    /// <param name="category">The committed change category.</param>
    /// <returns>The manifest entry.</returns>
    internal static SnapshotManifestEntry CreateEntry(
        TemporaryGitRepository repository,
        string mergeBase,
        string head,
        string mergeBasePath,
        string headPath,
        SnapshotChangeCategory category)
    {
        var mergeBaseObjectId = ResolveBlob(repository, mergeBase, mergeBasePath);
        var headObjectId = ResolveBlob(repository, head, headPath);
        return new SnapshotManifestEntry(headPath, mergeBasePath, category, "100644", "100644", mergeBaseObjectId, headObjectId);
    }

    /// <summary>
    ///     Resolves the blob object identifier of a path at a revision.
    /// </summary>
    /// <param name="repository">The fixture repository. Cannot be <see langword="null" />.</param>
    /// <param name="revision">The revision to read. Cannot be <see langword="null" />.</param>
    /// <param name="path">The repository-relative path. Cannot be <see langword="null" />.</param>
    /// <returns>The blob object identifier.</returns>
    internal static string ResolveBlob(TemporaryGitRepository repository, string revision, string path)
    {
        var output = TemporaryGitRepository.RunGit(["-C", repository.RootPath, "rev-parse", $"{revision}:{path}"]);
        Assert.Equal(0, output.ExitCode);
        return output.StandardOutput.Trim();
    }
}
