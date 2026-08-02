using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Core.Snapshots.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Snapshots;

/// <summary>
///     Verifies real-Git committed snapshot capture semantics and manifest-hash behavior.
/// </summary>
public sealed class GitSnapshotCaptureServiceTests
{
    /// <summary>
    ///     Asynchronously captures renames, deletions, additions, and type changes with their exact Git facts.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CaptureAsyncRecordsEveryCommittedCategoryWithGitFacts()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("kept.txt", "kept\n", "add kept");
        repository.CommitFile("removed.txt", "removed\n", "add removed");
        repository.CommitFile("link-source.txt", "link\n", "add link source");
        repository.CreateLocalBranch("topic");
        repository.Move("kept.txt", "renamed.txt");
        repository.Remove("removed.txt");
        repository.ReplaceWithSymbolicLink("link-source.txt", "renamed.txt");
        repository.CommitFile("added.txt", "added\n", "rename, delete, retype, and add");

        var result = await CaptureAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        var entries = result.Data!.Manifest.Entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        Assert.Equal(SnapshotChangeCategory.Renamed, entries["renamed.txt"].Category);
        Assert.Equal("kept.txt", entries["renamed.txt"].OriginalPath);
        Assert.Equal(SnapshotChangeCategory.Deleted, entries["removed.txt"].Category);
        Assert.Equal("000000", entries["removed.txt"].HeadEntryMode);
        Assert.Equal(new string('0', entries["removed.txt"].MergeBaseObjectId.Length), entries["removed.txt"].HeadObjectId);
        Assert.Equal(SnapshotChangeCategory.Added, entries["added.txt"].Category);
        Assert.Equal("000000", entries["added.txt"].MergeBaseEntryMode);
        Assert.Equal(SnapshotChangeCategory.TypeChanged, entries["link-source.txt"].Category);
        Assert.Equal("120000", entries["link-source.txt"].HeadEntryMode);
        Assert.Equal(0, result.Data.ExcludedUncommittedCounts.Total);
    }

    /// <summary>
    ///     Asynchronously counts one staged-and-unstaged lineage once while keeping it out of the manifest.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CaptureAsyncCountsOverlappingUncommittedLineageOnceAndExcludesIt()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.CommitFile("committed.txt", "committed\n", "committed work");
        repository.WriteFile("live.txt", "staged\n");
        repository.Stage("live.txt");
        repository.WriteFile("live.txt", "staged then edited\n");
        repository.WriteFile("fresh.txt", "untracked\n");

        var result = await CaptureAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.ExcludedUncommittedCounts.Total);
        Assert.Equal(1, result.Data.ExcludedUncommittedCounts.Staged);
        Assert.Equal(1, result.Data.ExcludedUncommittedCounts.Unstaged);
        Assert.Equal(1, result.Data.ExcludedUncommittedCounts.Untracked);
        Assert.DoesNotContain(result.Data.Manifest.Entries, entry => entry.Path is "live.txt" or "fresh.txt");
    }

    /// <summary>
    ///     Asynchronously proves the manifest hash covers committed content and ignores excluded counts.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CaptureAsyncManifestHashIsStableAcrossCapturesAndIgnoresExcludedCounts()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.CommitFile("committed.txt", "committed\n", "committed work");

        var first = await CaptureAsync(repository, "refs/heads/topic");
        repository.WriteFile("live.txt", "untracked\n");
        var second = await CaptureAsync(repository, "refs/heads/topic");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Data!.Manifest.ManifestHash, second.Data!.Manifest.ManifestHash);
        Assert.NotEqual(first.Data.Manifest.SnapshotId, second.Data.Manifest.SnapshotId);
        Assert.Equal(64, first.Data.Manifest.ManifestHash.Length);
        Assert.DoesNotContain(first.Data.Manifest.ManifestHash, character => !char.IsAsciiDigit(character) &&
            !char.IsAsciiLetterLower(character));
        Assert.Equal(0, first.Data.ExcludedUncommittedCounts.Total);
        Assert.Equal(1, second.Data.ExcludedUncommittedCounts.Total);
    }

    /// <summary>
    ///     Asynchronously proves an added committed file changes the manifest hash.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CaptureAsyncManifestHashChangesWithCommittedContent()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.CommitFile("first.txt", "first\n", "first commit");

        var before = await CaptureAsync(repository, "refs/heads/topic");
        repository.CommitFile("second.txt", "second\n", "second commit");
        var after = await CaptureAsync(repository, "refs/heads/topic");

        Assert.True(before.IsSuccess);
        Assert.True(after.IsSuccess);
        Assert.NotEqual(before.Data!.Manifest.ManifestHash, after.Data!.Manifest.ManifestHash);
    }

    /// <summary>
    ///     Asynchronously fails the run as stale when the accepted HEAD revision no longer matches.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CaptureAsyncStaleHeadRevisionFailsWithStaleAtCapture()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.CommitFile("committed.txt", "committed\n", "committed work");
        var acceptedHead = repository.Revision;
        repository.CommitFile("moved.txt", "moved\n", "head moves after acceptance");

        var result = await CaptureAsync(repository, "refs/heads/topic", acceptedHead);

        Assert.True(result.IsFailure);
        Assert.Equal(AnalysisFailureCode.StaleAtCapture, result.Errors[0].Code);
    }

    /// <summary>
    ///     Asynchronously captures a SHA-256 repository with full-length object identifiers.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CaptureAsyncSupportsSha256ObjectIdentifiers()
    {
        Assert.SkipUnless(TemporaryGitRepository.SupportsObjectFormat("sha256"),
            "The installed Git does not support the sha256 object format.");
        using var repository = new TemporaryGitRepository(objectFormat: "sha256");
        repository.CreateLocalBranch("topic");
        repository.CommitFile("committed.txt", "committed\n", "committed work");

        var result = await CaptureAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        Assert.All(result.Data!.Manifest.Entries, entry => Assert.Equal(64, entry.HeadObjectId.Length));
    }

    private static async Task<ChangeLens.Core.Results.Models.Result<SnapshotCapture>> CaptureAsync(
        TemporaryGitRepository repository,
        string target,
        string? acceptedHeadRevision = null)
    {
        var before = RepositoryStateSnapshot.Capture(repository.RootPath);
        var runner = new GitCliCommandRunner();
        var service = new GitSnapshotCaptureService(runner, new ComparisonFileSummaryComposer(),
            NullLogger<GitSnapshotCaptureService>.Instance);
        var targetRevision = TemporaryGitRepository
            .RunGit(["-C", repository.RootPath, "rev-parse", "--verify", target + "^{commit}"]).StandardOutput.Trim();
        var run = new AnalysisRunDetail(Guid.NewGuid(), AnalysisRunState.Capturing,
            new AnalysisRepositoryIdentity(Guid.NewGuid(), "fixture", repository.RootPath, repository.RootPath,
                acceptedHeadRevision ?? repository.Revision),
            new AnalysisComparisonIdentity(target, targetRevision, new string('0', 64)), 1_000, 1_100, false, null, null, null);

        var result = await service.CaptureAsync(run, TestContext.Current.CancellationToken);

        var after = RepositoryStateSnapshot.Capture(repository.RootPath);
        Assert.Equal(before.PorcelainStatus, after.PorcelainStatus);
        Assert.Equal(before.FileHashes, after.FileHashes);
        return result;
    }
}
