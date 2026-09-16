using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Constants;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Core.Snapshots.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Snapshots;

/// <summary>
///     Verifies bounded frozen Git reads against real temporary repositories.
/// </summary>
public sealed class FrozenGitTreeReaderIntegrationTests
{
    /// <summary>
    ///     Asynchronously lists the captured tree and reads a blob returned by that listing.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ListTreeAsync_AndReadBlobAsync_UseCapturedTreeIdentity()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("second.txt", "second content\n", "add second file");
        var snapshot = CreateSnapshot(repository, repository.Revision, repository.Revision, []);
        var reader = OpenReader(repository, snapshot);

        var listingResult = await reader.ListTreeAsync(TestContext.Current.CancellationToken);
        Assert.True(listingResult.IsSuccess);
        var listing = Assert.IsType<FrozenGitTreeListing>(listingResult.Data);
        var file = Assert.Single(listing.Files, candidate => candidate.Path == "second.txt");

        var blobResult = await reader.ReadBlobAsync(file.ObjectId, TestContext.Current.CancellationToken);
        Assert.True(blobResult.IsSuccess);
        var blob = Assert.IsType<FrozenGitBlob>(blobResult.Data);
        Assert.Equal(["second content"], blob.Lines);
    }

    /// <summary>
    ///     Asynchronously reads the exact manifest blobs and returns their changed lines after worktree edits.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ReadBlobAndDiffAsync_IgnoreWorktreeEditsAfterCapture()
    {
        using var repository = new TemporaryGitRepository();
        var mergeBase = repository.Revision;
        repository.CommitFile("fixture.txt", "captured content\n", "change captured file");
        var head = repository.Revision;
        var entry = CreateEntry(repository, mergeBase, head, "fixture.txt", "fixture.txt");
        var reader = OpenReader(repository, CreateSnapshot(repository, mergeBase, head, [entry]));
        repository.WriteFile("fixture.txt", "worktree-only content\n");

        var blobResult = await reader.ReadBlobAsync(entry.HeadObjectId, TestContext.Current.CancellationToken);
        var diffResult = await reader.ReadBlobDiffAsync(entry, TestContext.Current.CancellationToken);

        Assert.True(blobResult.IsSuccess);
        Assert.Equal(["captured content"], Assert.IsType<FrozenGitBlob>(blobResult.Data).Lines);
        Assert.True(diffResult.IsSuccess);
        var diff = Assert.IsType<FrozenGitBlobDiff>(diffResult.Data);
        Assert.Equal(["captured content"], diff.AddedLines.Select(line => line.Content));
        Assert.Equal(["initial fixture content"], diff.RemovedLines.Select(line => line.Content));
    }

    /// <summary>
    ///     Asynchronously ignores a Git replace ref when reading a captured blob identity.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ReadBlobAsync_IgnoresGitReplaceObject()
    {
        using var repository = new TemporaryGitRepository();
        var capturedRevision = repository.Revision;
        var capturedObjectId = ResolveBlob(repository, capturedRevision, "fixture.txt");
        repository.CommitFile("replacement.txt", "replacement content\n", "add replacement object");
        var replacementObjectId = ResolveBlob(repository, repository.Revision, "replacement.txt");
        var replaceResult = TemporaryGitRepository.RunGit(
            ["-C", repository.RootPath, "replace", capturedObjectId, replacementObjectId]);
        Assert.Equal(0, replaceResult.ExitCode);

        var snapshot = CreateSnapshot(
            repository,
            repository.Revision,
            repository.Revision,
            [new SnapshotManifestEntry(
                "fixture.txt", null, SnapshotChangeCategory.Modified, "100644", "100644", capturedObjectId, capturedObjectId)]);
        var reader = OpenReader(repository, snapshot);

        var result = await reader.ReadBlobAsync(capturedObjectId, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["initial fixture content"], Assert.IsType<FrozenGitBlob>(result.Data).Lines);
    }

    /// <summary>
    ///     Asynchronously ignores inherited Git repository and object selectors for frozen reads.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ReadBlobAsync_IgnoresInheritedGitRepositorySelectors()
    {
        using var repository = new TemporaryGitRepository("captured-repository");
        using var hostileRepository = new TemporaryGitRepository("hostile-repository");
        var revision = repository.Revision;
        var objectId = ResolveBlob(repository, revision, "fixture.txt");
        var snapshot = CreateSnapshot(
            repository,
            revision,
            revision,
            [new SnapshotManifestEntry("fixture.txt", null, SnapshotChangeCategory.Modified, "100644", "100644", objectId, objectId)]);
        var reader = OpenReader(repository, snapshot);
        var hostileGitDirectory = Path.Combine(hostileRepository.RootPath, ".git");
        var hostileSelectors = new Dictionary<string, string?>
        {
            ["GIT_DIR"] = hostileGitDirectory,
            ["GIT_WORK_TREE"] = hostileRepository.RootPath,
            ["GIT_COMMON_DIR"] = hostileGitDirectory,
            ["GIT_INDEX_FILE"] = Path.Combine(hostileGitDirectory, "index"),
            ["GIT_OBJECT_DIRECTORY"] = Path.Combine(hostileGitDirectory, "objects"),
            ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = Path.Combine(hostileGitDirectory, "objects"),
            ["GIT_NAMESPACE"] = "hostile",
            ["GIT_SHALLOW_FILE"] = Path.Combine(hostileGitDirectory, "shallow"),
            ["GIT_CEILING_DIRECTORIES"] = hostileRepository.RootPath,
            ["GIT_DISCOVERY_ACROSS_FILESYSTEM"] = "0",
            ["GIT_NO_REPLACE_OBJECTS"] = "0",
            ["GIT_REPLACE_REF_BASE"] = "refs/replace/hostile/",
        };

        var result = await RunWithEnvironmentAsync(
            hostileSelectors,
            () => reader.ReadBlobAsync(objectId, TestContext.Current.CancellationToken));

        Assert.True(result.IsSuccess);
        Assert.Equal(["initial fixture content"], Assert.IsType<FrozenGitBlob>(result.Data).Lines);
    }

    /// <summary>
    ///     Asynchronously returns a stale failure when a manifest identity is unavailable in the repository.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ReadBlobAsync_MissingCapturedObject_ReturnsStaleFailure()
    {
        using var repository = new TemporaryGitRepository();
        var missingObjectId = new string('f', 40);
        var snapshot = CreateSnapshot(
            repository,
            repository.Revision,
            repository.Revision,
            [new SnapshotManifestEntry("missing.txt", null, SnapshotChangeCategory.Added, "000000", "100644", new string('0', 40), missingObjectId)]);
        var reader = OpenReader(repository, snapshot);

        var result = await reader.ReadBlobAsync(missingObjectId, TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.Conflict, SnapshotErrorCode.StaleObject);
    }

    /// <summary>
    ///     Rejects malformed manifest object identities while retaining valid zero sentinels for one-sided entries.
    /// </summary>
    /// <param name="invalidObjectId">The malformed manifest identity.</param>
    [Theory]
    [InlineData("HEAD")]
    [InlineData("abc123")]
    [InlineData("--no-replace-objects")]
    [InlineData("000000000000000000000000000000000000000")]
    [InlineData("00000000000000000000000000000000000000000")]
    public void Open_InvalidManifestObjectIdentity_ReturnsValidationFailure(string invalidObjectId)
    {
        using var repository = new TemporaryGitRepository();
        var revision = repository.Revision;
        var objectId = ResolveBlob(repository, revision, "fixture.txt");
        var snapshot = CreateSnapshot(
            repository,
            revision,
            revision,
            [new SnapshotManifestEntry("fixture.txt", null, SnapshotChangeCategory.Modified, "100644", "100644", invalidObjectId, objectId)]);
        var result = OpenReaderResult(repository, snapshot);

        AssertFailure(result, ErrorType.Validation, SnapshotErrorCode.InvalidSnapshot);
    }

    /// <summary>
    ///     Rejects a snapshot whose repository and every captured identity use a non-Git hash length.
    /// </summary>
    [Fact]
    public void Open_InvalidShortRepositoryAndSnapshotIdentities_ReturnsValidationFailure()
    {
        using var repository = new TemporaryGitRepository();
        var invalidRevision = new string('a', 39);
        var snapshot = CreateSnapshot(
            repository,
            invalidRevision,
            invalidRevision,
            [new SnapshotManifestEntry(
                "fixture.txt",
                "fixture.txt",
                SnapshotChangeCategory.Modified,
                "100644",
                "100644",
                invalidRevision,
                invalidRevision)]);

        var result = OpenReaderResult(repository, snapshot);

        AssertFailure(result, ErrorType.Validation, SnapshotErrorCode.InvalidSnapshot);
    }

    /// <summary>
    ///     Accepts correctly-sized all-zero identities for added and deleted manifest entries.
    /// </summary>
    [Fact]
    public void Open_OneSidedManifestEntries_AcceptsZeroSentinels()
    {
        using var repository = new TemporaryGitRepository();
        var revision = repository.Revision;
        var objectId = ResolveBlob(repository, revision, "fixture.txt");
        var zero = new string('0', objectId.Length);
        var snapshot = CreateSnapshot(
            repository,
            revision,
            revision,
            [
                new SnapshotManifestEntry("added.txt", null, SnapshotChangeCategory.Added, zero[..6], "100644", zero, objectId),
                new SnapshotManifestEntry("fixture.txt", null, SnapshotChangeCategory.Deleted, "100644", zero[..6], objectId, zero),
            ]);

        var result = OpenReaderResult(repository, snapshot);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    ///     Asynchronously records binary and oversized content as explicit skip reasons.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ReadBlobAsync_RecordsBinaryAndOversizedReasons()
    {
        using var repository = new TemporaryGitRepository();
        repository.WriteFile("binary.bin", "before\n");
        repository.CommitFile("binary.bin", "before\n", "add binary seed");
        var binaryBase = repository.Revision;
        var binaryPath = Path.Combine(repository.RootPath, "binary.bin");
        await File.WriteAllBytesAsync(binaryPath, [0, 1, 2, 3], TestContext.Current.CancellationToken);
        repository.Stage("binary.bin");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "binary update"]);
        var binaryHead = repository.Revision;
        var binaryEntry = CreateEntry(repository, binaryBase, binaryHead, "binary.bin", "binary.bin");

        var binaryReader = OpenReader(repository, CreateSnapshot(repository, binaryBase, binaryHead, [binaryEntry]));
        var binaryResult = await binaryReader.ReadBlobAsync(binaryEntry.HeadObjectId, TestContext.Current.CancellationToken);
        Assert.True(binaryResult.IsSuccess);
        Assert.Equal(FrozenGitBlobSkipReason.Binary, Assert.IsType<FrozenGitBlob>(binaryResult.Data).SkipReason);

        repository.CommitFile("large.txt", "123456789\n", "add large file");
        var largeEntry = CreateAddedEntry(repository, repository.Revision, "large.txt");
        var options = new FrozenGitTreeReaderOptions { MaximumBlobBytes = 5 };
        var largeReader = OpenReader(repository, CreateSnapshot(repository, binaryHead, repository.Revision, [largeEntry]), options);
        var largeResult = await largeReader.ReadBlobAsync(largeEntry.HeadObjectId, TestContext.Current.CancellationToken);

        Assert.True(largeResult.IsSuccess);
        Assert.Equal(FrozenGitBlobSkipReason.TooLarge, Assert.IsType<FrozenGitBlob>(largeResult.Data).SkipReason);
    }

    /// <summary>
    ///     Asynchronously enforces the configured tree and history commit bounds.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ListTreeAsync_AndReadHistoryAsync_EnforceConfiguredBounds()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("second.txt", "second\n", "second");
        repository.CommitFile("third.txt", "third\n", "third");
        var snapshot = CreateSnapshot(repository, repository.Revision, repository.Revision, []);
        var reader = OpenReader(repository, snapshot, new FrozenGitTreeReaderOptions
        {
            MaximumTreeFiles = 1,
            MaximumHistoryCommits = 1,
        });

        var treeResult = await reader.ListTreeAsync(TestContext.Current.CancellationToken);
        var historyResult = await reader.ReadHistoryAsync(TestContext.Current.CancellationToken);

        Assert.True(treeResult.IsSuccess);
        Assert.True(Assert.IsType<FrozenGitTreeListing>(treeResult.Data).WasTruncated);
        Assert.True(historyResult.IsSuccess);
        var history = Assert.IsType<FrozenGitHistoryScan>(historyResult.Data);
        Assert.Equal(1, history.CommitsInspected);
        Assert.True(history.WasTruncated);
    }

    /// <summary>
    ///     Asynchronously ignores a history commit that touches more paths than the configured cap.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ReadHistoryAsync_SkipsCommitThatTouchesTooManyPaths()
    {
        using var repository = new TemporaryGitRepository();
        var paths = Enumerable.Range(0, 3).Select(index => $"bulk-{index}.txt").ToArray();
        foreach (var path in paths)
        {
            repository.WriteFile(path, "bulk\n");
        }

        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "add", "--", "."]);
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "bulk change"]);
        var snapshot = CreateSnapshot(repository, repository.Revision, repository.Revision, []);
        var reader = OpenReader(repository, snapshot, new FrozenGitTreeReaderOptions
        {
            MaximumHistoryCommits = 1,
            MaximumHistoryPathsPerCommit = 2,
        });

        var result = await reader.ReadHistoryAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var history = Assert.IsType<FrozenGitHistoryScan>(result.Data);
        Assert.Equal(1, history.CommitsInspected);
        Assert.Equal(1, history.OversizedCommitsSkipped);
        Assert.Empty(history.Commits);
    }

    private static IFrozenGitTreeReader OpenReader(
        TemporaryGitRepository repository,
        SnapshotManifest snapshot,
        FrozenGitTreeReaderOptions? options = null)
    {
        var key = Path.GetFullPath(repository.RootPath);
        var identity = new AnalysisRepositoryIdentity(Guid.NewGuid(), "fixture", key, key, snapshot.HeadRevision);
        var result = OpenReaderResult(repository, snapshot, options);
        Assert.True(result.IsSuccess);
        return Assert.IsAssignableFrom<IFrozenGitTreeReader>(result.Data);
    }

    private static Result<IFrozenGitTreeReader> OpenReaderResult(
        TemporaryGitRepository repository,
        SnapshotManifest snapshot,
        FrozenGitTreeReaderOptions? options = null)
    {
        var key = Path.GetFullPath(repository.RootPath);
        var identity = new AnalysisRepositoryIdentity(Guid.NewGuid(), "fixture", key, key, snapshot.HeadRevision);
        return new FrozenGitTreeReaderFactory(new GitCliCommandRunner(), options ?? new FrozenGitTreeReaderOptions()).Open(identity, snapshot);
    }

    private static SnapshotManifest CreateSnapshot(
        TemporaryGitRepository repository,
        string mergeBase,
        string head,
        IReadOnlyList<SnapshotManifestEntry> entries) =>
        new(Guid.NewGuid(), new string('a', 64), Path.GetFullPath(repository.RootPath), "fixture", head, head, mergeBase, entries);

    private static SnapshotManifestEntry CreateEntry(
        TemporaryGitRepository repository,
        string mergeBase,
        string head,
        string mergeBasePath,
        string headPath)
    {
        var mergeBaseObjectId = ResolveBlob(repository, mergeBase, mergeBasePath);
        var headObjectId = ResolveBlob(repository, head, headPath);
        return new SnapshotManifestEntry(
            headPath, mergeBasePath, SnapshotChangeCategory.Modified, "100644", "100644", mergeBaseObjectId, headObjectId);
    }

    private static SnapshotManifestEntry CreateAddedEntry(
        TemporaryGitRepository repository,
        string head,
        string path)
    {
        var headObjectId = ResolveBlob(repository, head, path);
        var objectLength = headObjectId.Length;
        return new SnapshotManifestEntry(
            path, null, SnapshotChangeCategory.Added, new string('0', 6), "100644", new string('0', objectLength), headObjectId);
    }

    private static string ResolveBlob(TemporaryGitRepository repository, string revision, string path)
    {
        var output = TemporaryGitRepository.RunGit(["-C", repository.RootPath, "rev-parse", $"{revision}:{path}"]);
        Assert.Equal(0, output.ExitCode);
        return output.StandardOutput.Trim();
    }

    private static void AssertFailure<T>(Result<T> result, ErrorType type, string code)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(type, error.Type);
        Assert.Equal(code, error.Code);
    }

    private static async Task<T> RunWithEnvironmentAsync<T>(
        IReadOnlyDictionary<string, string?> values,
        Func<Task<T>> operation)
    {
        var previousValues = values.Keys.ToDictionary(
            key => key,
            Environment.GetEnvironmentVariable,
            StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        try
        {
            return await operation();
        }
        finally
        {
            foreach (var (key, value) in previousValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
