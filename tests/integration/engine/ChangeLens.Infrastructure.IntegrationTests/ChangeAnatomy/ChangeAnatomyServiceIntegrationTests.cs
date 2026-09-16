using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ChangeAnatomy.Interfaces;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ChangeAnatomy.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Core.Snapshots.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Anatomy;

/// <summary>
///     Verifies change anatomy against real frozen Git fixtures.
/// </summary>
public sealed class ChangeAnatomyServiceIntegrationTests
{
    /// <summary>
    ///     Asynchronously extracts keys for added, modified, deleted, and renamed committed files.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task AnalyzeAsync_ExtractsKeysForAllCommittedChangeCategories()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("deleted.txt", "old deleted marker\n", "seed deleted file");
        repository.CommitFile("modified.txt", "old modified marker\n", "seed modified file");
        repository.CommitFile("old-name.cs", "class OldName { }\n", "seed rename file");
        var mergeBase = repository.Revision;

        repository.WriteFile("added.cs", "class AddedName { string route = \"added-route\"; }\n");
        repository.WriteFile("modified.txt", "new modified marker\n");
        repository.Move("old-name.cs", "new-name.cs");
        repository.Remove("deleted.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "add", "--", "added.cs", "modified.txt"]);
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "apply change categories"]);
        var head = repository.Revision;
        var added = CreateAddedEntry(repository, head, "added.cs");
        var modified = CreateEntry(repository, mergeBase, head, "modified.txt", "modified.txt", SnapshotChangeCategory.Modified);
        var deleted = CreateDeletedEntry(repository, mergeBase, "deleted.txt");
        var renamed = CreateEntry(repository, mergeBase, head, "old-name.cs", "new-name.cs", SnapshotChangeCategory.Renamed);
        var result = await AnalyzeAsync(repository, mergeBase, head, [added, modified, deleted, renamed]);

        Assert.True(result.IsSuccess);
        var anatomy = Assert.IsType<ChangeAnatomy>(result.Data);
        Assert.Equal(4, anatomy.Diagnostics.ChangedFileCount);
        Assert.Equal(4, anatomy.Diagnostics.AnalyzedFileCount);
        Assert.All(anatomy.Files, file => Assert.Null(file.SkipReason));
        Assert.Contains(anatomy.Files.Single(file => file.Path == "added.cs").Keys,
            key => key.Normalized == "addedname" && key.Kind == ChangeAnatomyKeyKind.Identifier);
        Assert.Contains(anatomy.Files.Single(file => file.Path == "added.cs").Keys,
            key => key.Normalized == "added-route" && key.Kind == ChangeAnatomyKeyKind.StringLiteral);
        Assert.Contains(anatomy.Files.Single(file => file.Path == "modified.txt").Keys,
            key => key.Normalized == "new" && key.Kind == ChangeAnatomyKeyKind.Identifier);
        Assert.Contains(anatomy.Files.Single(file => file.Path == "deleted.txt").Keys,
            key => key.Normalized == "old" && key.Kind == ChangeAnatomyKeyKind.Identifier);
        var renameKeys = anatomy.Files.Single(file => file.Path == "new-name.cs").Keys;
        Assert.Contains(renameKeys, key => key.Normalized == "old-name" && key.Kind == ChangeAnatomyKeyKind.PathStem);
        Assert.Contains(renameKeys, key => key.Normalized == "new-name" && key.Kind == ChangeAnatomyKeyKind.PathStem);
        Assert.Equal(0, anatomy.Diagnostics.SkippedFileCount);
    }

    /// <summary>
    ///     Asynchronously skips excluded paths when either side of a rename is excluded.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task AnalyzeAsync_RenameIsSkippedWhenEitherPathIsExcluded()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("vendor/old.cs", "class VendorOld { }\n", "seed old excluded path");
        repository.CommitFile("safe.cs", "class Safe { }\n", "seed new excluded path");
        repository.CommitFile("lock.txt", "lock seed\n", "seed lock path");
        repository.CommitFile("credential.txt", "credential seed\n", "seed credential path");
        var mergeBase = repository.Revision;
        repository.Move("vendor/old.cs", "renamed.cs");
        repository.Move("safe.cs", "vendor/new.cs");
        repository.WriteFile("package-lock.json", "{ \"packages\": {} }\n");
        repository.WriteFile("credential.txt", "new credential\n");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "add", "--", "package-lock.json", "credential.txt"]);
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "apply exclusions"]);
        var head = repository.Revision;
        var firstRename = CreateEntry(repository, mergeBase, head, "vendor/old.cs", "renamed.cs", SnapshotChangeCategory.Renamed);
        var secondRename = CreateEntry(repository, mergeBase, head, "safe.cs", "vendor/new.cs", SnapshotChangeCategory.Renamed);
        var lockEntry = CreateAddedEntry(repository, head, "package-lock.json");
        var credentialEntry = CreateEntry(repository, mergeBase, head, "credential.txt", "credential.txt", SnapshotChangeCategory.Modified);
        var result = await AnalyzeAsync(repository, mergeBase, head, [firstRename, secondRename, lockEntry, credentialEntry]);

        Assert.True(result.IsSuccess);
        var anatomy = Assert.IsType<ChangeAnatomy>(result.Data);
        Assert.Equal(4, anatomy.Diagnostics.SkippedFileCount);
        Assert.Equal("inside 'vendor'", anatomy.Files.Single(file => file.Path == "renamed.cs").SkipReason);
        Assert.Equal("inside 'vendor'", anatomy.Files.Single(file => file.Path == "vendor/new.cs").SkipReason);
        Assert.Equal("lock, credential, or secret file", anatomy.Files.Single(file => file.Path == "package-lock.json").SkipReason);
        Assert.Equal("lock, credential, or secret file", anatomy.Files.Single(file => file.Path == "credential.txt").SkipReason);
    }

    /// <summary>
    ///     Asynchronously records binary and submodule skips while continuing through the manifest.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task AnalyzeAsync_BinaryAndSubmoduleEntriesAreSkipped()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("text.txt", "before text\n", "seed text");
        var mergeBase = repository.Revision;
        var binaryPath = Path.Combine(repository.RootPath, "data.dat");
        await File.WriteAllBytesAsync(binaryPath, [0, 1, 2, 3], TestContext.Current.CancellationToken);
        repository.Stage("data.dat");
        repository.CreateSubmodule();
        var head = repository.Revision;
        var binaryEntry = CreateAddedEntry(repository, head, "data.dat");
        var submodulePath = "child module";
        var submoduleRevision = TemporaryGitRepository.RunGit(["-C", Path.Combine(repository.RootPath, submodulePath), "rev-parse", "HEAD"])
            .StandardOutput.Trim();
        var zero = new string('0', submoduleRevision.Length);
        var submoduleEntry = new SnapshotManifestEntry(
            submodulePath, null, SnapshotChangeCategory.Added, "000000", "160000", zero, submoduleRevision);

        var result = await AnalyzeAsync(repository, mergeBase, head, [binaryEntry, submoduleEntry]);

        Assert.True(result.IsSuccess);
        var anatomy = Assert.IsType<ChangeAnatomy>(result.Data);
        Assert.Equal("binary content", anatomy.Files.Single(file => file.Path == "data.dat").SkipReason);
        Assert.Equal("submodule pointer", anatomy.Files.Single(file => file.Path == submodulePath).SkipReason);
        Assert.Equal(2, anatomy.Diagnostics.SkippedFileCount);
    }

    /// <summary>
    ///     Asynchronously preserves lexical state opened on unchanged lines and emits keys only on touched lines.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task AnalyzeAsync_CarriesCommentAndLiteralStateAcrossUnchangedLines()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile(
            "state.txt",
            "/* retained comment\noldMarker\n*/\n@\"retained literal\noldLiteral\n\";\n",
            "seed multiline state");
        var mergeBase = repository.Revision;
        repository.WriteFile(
            "state.txt",
            "/* retained comment\nchangedMarker\n*/\n@\"retained literal\nchangedLiteral\n\";\n");
        repository.Stage("state.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "change multiline state"]);
        var head = repository.Revision;
        var entry = CreateEntry(repository, mergeBase, head, "state.txt", "state.txt", SnapshotChangeCategory.Modified);
        var result = await AnalyzeAsync(repository, mergeBase, head, [entry]);

        Assert.True(result.IsSuccess);
        var file = Assert.Single(Assert.IsType<ChangeAnatomy>(result.Data).Files);
        Assert.Contains(file.Keys, key => key.Normalized == "changedmarker" && key.Kind == ChangeAnatomyKeyKind.CommentWord);
        Assert.DoesNotContain(file.Keys, key => key.Normalized == "changedmarker" && key.Kind == ChangeAnatomyKeyKind.Identifier);
        Assert.Contains(file.Keys, key => key.Normalized == "changedliteral" && key.Kind == ChangeAnatomyKeyKind.StringLiteral);
        Assert.Contains(file.Keys, key => key.Normalized == "changedliteral" && key.Kind == ChangeAnatomyKeyKind.Identifier);
    }

    /// <summary>
    ///     Asynchronously reports key-cap truncation and analyzed files with zero keys.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task AnalyzeAsync_ReportsTruncationAndZeroKeyWarning()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("a", "\n", "seed zero key file");
        repository.CommitFile("cap.txt", "old\n", "seed cap file");
        var mergeBase = repository.Revision;
        repository.WriteFile("a", "!!!\n");
        repository.WriteFile("cap.txt", "firstKey secondKey thirdKey\n");
        repository.Stage("a");
        repository.Stage("cap.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "change caps"]);
        var head = repository.Revision;
        var zeroEntry = CreateEntry(repository, mergeBase, head, "a", "a", SnapshotChangeCategory.Modified);
        var capEntry = CreateEntry(repository, mergeBase, head, "cap.txt", "cap.txt", SnapshotChangeCategory.Modified);
        var logger = new RecordingSnapshotLogger<ChangeAnatomyService>();
        var result = await AnalyzeAsync(
            repository,
            mergeBase,
            head,
            [zeroEntry, capEntry],
            new ChangeAnatomyOptions { MaximumKeysPerFile = 2 },
            logger);

        Assert.True(result.IsSuccess);
        var anatomy = Assert.IsType<ChangeAnatomy>(result.Data);
        Assert.True(anatomy.Files.Single(file => file.Path == "cap.txt").Truncated);
        Assert.True(anatomy.Files.Single(file => file.Path == "a").HasZeroKeys);
        Assert.Equal(1, anatomy.Diagnostics.TruncatedFileCount);
        Assert.Equal(1, anatomy.Diagnostics.ZeroKeyFileCount);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("zero keys", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("truncated", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Asynchronously propagates a missing captured object as a stale failure.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task AnalyzeAsync_MissingCapturedObjectReturnsStaleFailure()
    {
        using var repository = new TemporaryGitRepository();
        var revision = repository.Revision;
        var missingObjectId = new string('f', revision.Length);
        var entry = new SnapshotManifestEntry(
            "missing.txt", null, SnapshotChangeCategory.Added, "000000", "100644", new string('0', revision.Length), missingObjectId);

        var result = await AnalyzeAsync(repository, revision, revision, [entry]);

        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal("snapshot.staleObject", error.Code);
    }

    /// <summary>
    ///     Asynchronously reads captured content after the worktree has been changed.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task AnalyzeAsync_IgnoresWorktreeMutationAfterCapture()
    {
        using var repository = new TemporaryGitRepository();
        var mergeBase = repository.Revision;
        repository.CommitFile("captured.txt", "capturedName = \"captured-value\";\n", "capture content");
        var head = repository.Revision;
        var entry = CreateAddedEntry(repository, head, "captured.txt");
        repository.WriteFile("captured.txt", "worktreeName = \"worktree-value\";\n");

        var result = await AnalyzeAsync(repository, mergeBase, head, [entry]);

        Assert.True(result.IsSuccess);
        var file = Assert.Single(Assert.IsType<ChangeAnatomy>(result.Data).Files);
        Assert.Contains(file.Keys, key => key.Normalized == "capturedname");
        Assert.Contains(file.Keys, key => key.Normalized == "captured-value");
        Assert.DoesNotContain(file.Keys, key => key.Normalized == "worktreename");
    }

    /// <summary>
    ///     Asynchronously observes cancellation before opening an empty captured snapshot.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task AnalyzeAsync_AlreadyCancelledEmptySnapshotThrowsBeforeReaderOpen()
    {
        using var repository = new TemporaryGitRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => AnalyzeAsync(repository, repository.Revision, repository.Revision, [], cancellationToken: cancellation.Token));
    }

    /// <summary>
    ///     Observes cancellation requested by an emission callback during a long lexical scan.
    /// </summary>
    [Fact]
    public void Tokenizer_ObservesCancellationDuringLongLine()
    {
        var tokenizer = new ChangeAnatomyTokenizer(3);
        var state = ChangeAnatomyLexicalState.ForPath("state.txt");
        using var cancellation = new CancellationTokenSource();
        var line = string.Join(' ', Enumerable.Range(0, 1_000).Select(index => $"token{index}"));

        Assert.Throws<OperationCanceledException>(() => tokenizer.TokenizeLine(
            line,
            1,
            ref state,
            (_, _, _, _) => cancellation.Cancel(),
            cancellation.Token));
    }

    private static async Task<Result<ChangeAnatomy>> AnalyzeAsync(
        TemporaryGitRepository repository,
        string mergeBase,
        string head,
        IReadOnlyList<SnapshotManifestEntry> entries,
        ChangeAnatomyOptions? options = null,
        ILogger<ChangeAnatomyService>? logger = null,
        CancellationToken? cancellationToken = null)
    {
        var key = Path.GetFullPath(repository.RootPath);
        var snapshot = new SnapshotManifest(Guid.NewGuid(), new string('a', 64), key, "fixture", head, head, mergeBase, entries);
        var identity = new AnalysisRepositoryIdentity(Guid.NewGuid(), "fixture", key, key, head);
        var readerFactory = new FrozenGitTreeReaderFactory(
            new GitCliCommandRunner(), new FrozenGitTreeReaderOptions(), NullLoggerFactory.Instance);
        var service = new ChangeAnatomyService(
            readerFactory, options ?? new ChangeAnatomyOptions(), logger ?? NullLogger<ChangeAnatomyService>.Instance);
        return await service.AnalyzeAsync(identity, snapshot, cancellationToken ?? TestContext.Current.CancellationToken);
    }

    private static SnapshotManifestEntry CreateAddedEntry(TemporaryGitRepository repository, string head, string path)
    {
        var objectId = ResolveBlob(repository, head, path);
        return new SnapshotManifestEntry(
            path,
            null,
            SnapshotChangeCategory.Added,
            new string('0', 6),
            "100644",
            new string('0', objectId.Length),
            objectId);
    }

    private static SnapshotManifestEntry CreateDeletedEntry(TemporaryGitRepository repository, string mergeBase, string path)
    {
        var objectId = ResolveBlob(repository, mergeBase, path);
        return new SnapshotManifestEntry(
            path,
            null,
            SnapshotChangeCategory.Deleted,
            "100644",
            new string('0', 6),
            objectId,
            new string('0', objectId.Length));
    }

    private static SnapshotManifestEntry CreateEntry(
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

    private static string ResolveBlob(TemporaryGitRepository repository, string revision, string path)
    {
        var output = TemporaryGitRepository.RunGit(["-C", repository.RootPath, "rev-parse", $"{revision}:{path}"]);
        Assert.Equal(0, output.ExitCode);
        return output.StandardOutput.Trim();
    }
}
