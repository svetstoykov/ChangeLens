using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Comparisons;

/// <summary>
///     Verifies comparison capacity boundaries and read-only repository evidence.
/// </summary>
public sealed class ComparisonCapacityTests
{
    /// <summary>
    ///     Verifies the repository snapshot separately detects mutations to every read-only evidence category.
    /// </summary>
    [Fact]
    public void RepositoryStateSnapshotDetectsEachTrackedCategoryMutation()
    {
        using var repository = new TemporaryGitRepository();

        var snapshot = RepositoryStateSnapshot.Capture(repository.RootPath);

        Assert.Equal(
            ["worktree", "head", "refs", "index", "config", "objects", "fetchHead", "porcelain"],
            snapshot.CategoryHashes.Keys);

        AssertCategoryChanges(
            repository,
            "worktree",
            () => repository.WriteFile("worktree-change.txt", "worktree\n"));
        AssertCategoryChanges(
            repository,
            "head",
            () =>
            {
                repository.CreateLocalBranch("head-state");
                repository.CheckoutBranch("head-state");
            });
        AssertCategoryChanges(
            repository,
            "refs",
            () => TemporaryGitRepository.RunGit(
                ["-C", repository.RootPath, "update-ref", "refs/heads/loose-ref", "HEAD"]));
        AssertCategoryChanges(
            repository,
            "refs",
            () => TemporaryGitRepository.RunGit(
                ["-C", repository.RootPath, "pack-refs", "--all", "--prune"]));
        AssertCategoryChanges(
            repository,
            "index",
            () =>
            {
                repository.WriteFile("indexed-change.txt", "index\n");
                repository.Stage("indexed-change.txt");
            });
        AssertCategoryChanges(
            repository,
            "config",
            () => TemporaryGitRepository.RunGit(
                ["-C", repository.RootPath, "config", "changelens.snapshot", "true"]));
        AssertCategoryChanges(
            repository,
            "objects",
            () =>
            {
                var objectDirectory = Path.Combine(repository.RootPath, ".git", "objects", "ff");
                Directory.CreateDirectory(objectDirectory);
                File.WriteAllText(Path.Combine(objectDirectory, "snapshot-evidence"), "object\n");
            });
        AssertCategoryChanges(
            repository,
            "fetchHead",
            () => File.WriteAllText(Path.Combine(repository.RootPath, ".git", "FETCH_HEAD"), "fetch\n"));
        AssertCategoryChanges(
            repository,
            "porcelain",
            () => repository.WriteFile("porcelain-change.txt", "status\n"));
    }

    /// <summary>
    ///     Verifies an access-time-only update does not change content-based repository evidence.
    /// </summary>
    [Fact]
    public void RepositoryStateSnapshotIgnoresAccessTimeOnlyMetadata()
    {
        using var repository = new TemporaryGitRepository();
        var filePath = Path.Combine(repository.RootPath, "fixture.txt");
        var before = RepositoryStateSnapshot.Capture(repository.RootPath);

        File.SetLastAccessTimeUtc(filePath, DateTime.UtcNow.AddMinutes(-5));

        var after = RepositoryStateSnapshot.Capture(repository.RootPath);
        Assert.Equal(before.CategoryHashes, after.CategoryHashes);
        Assert.Equal(before.FileHashes, after.FileHashes);
        Assert.Equal(before.PorcelainStatus, after.PorcelainStatus);
    }

    private static void AssertCategoryChanges(
        TemporaryGitRepository repository,
        string category,
        Action mutation)
    {
        var before = RepositoryStateSnapshot.Capture(repository.RootPath);

        mutation();

        var after = RepositoryStateSnapshot.Capture(repository.RootPath);
        Assert.NotEqual(before.CategoryHashes[category], after.CategoryHashes[category]);
    }
}
