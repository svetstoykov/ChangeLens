using System.Diagnostics;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Git.Services;

/// <summary>
///     Verifies real remote baseline detection and refresh against a local bare Git remote.
/// </summary>
public sealed class GitRemoteBaselineTrackerIntegrationTests
{
    /// <summary>
    ///     Asynchronously reports current immediately after the initial clone and fetch, with no colleague push.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CheckAsync_NoColleaguePush_ReturnsCurrent()
    {
        using var origin = TemporaryGitRepository.CreateBare();
        using var repository = new TemporaryGitRepository();
        repository.AddOrigin(origin);

        var result = await CreateTracker().CheckAsync(
            repository.RootPath,
            "refs/remotes/origin/main",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RemoteBaselineState.Current, result.Data!.State);
        Assert.Equal(repository.Revision, result.Data.RemoteRevision);
    }

    /// <summary>
    ///     Asynchronously reports moved after a colleague pushes to the bare origin behind the clone's back, and
    ///     changes nothing in the caller's repository.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CheckAsync_ColleaguePushedBehindOurBack_ReturnsMovedAndPreservesLocalState()
    {
        using var origin = TemporaryGitRepository.CreateBare();
        using var repository = new TemporaryGitRepository();
        repository.AddOrigin(origin);
        var newRevision = TemporaryGitRepository.PushBehindCallersBack(
            origin,
            "main",
            "colleague.txt",
            "colleague content\n",
            "colleague commit");

        var beforeHead = HeadRevision(repository.RootPath);
        var beforeMain = LocalRef(repository.RootPath, "refs/heads/main");
        var beforeStatus = PorcelainStatus(repository.RootPath);

        var result = await CreateTracker().CheckAsync(
            repository.RootPath,
            "refs/remotes/origin/main",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RemoteBaselineState.Moved, result.Data!.State);
        Assert.Equal(newRevision, result.Data.RemoteRevision);
        Assert.Equal(beforeHead, HeadRevision(repository.RootPath));
        Assert.Equal(beforeMain, LocalRef(repository.RootPath, "refs/heads/main"));
        Assert.Equal(beforeStatus, PorcelainStatus(repository.RootPath));
        Assert.Equal(repository.Revision, LocalRef(repository.RootPath, "refs/remotes/origin/main"));
    }

    /// <summary>
    ///     Asynchronously moves only the cached remote-tracking reference after a refresh, leaving local branches,
    ///     <c>HEAD</c>, the index, and the working tree unchanged.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RefreshAsync_AfterColleaguePush_MovesCachedRefAndPreservesLocalState()
    {
        using var origin = TemporaryGitRepository.CreateBare();
        using var repository = new TemporaryGitRepository();
        repository.AddOrigin(origin);
        var newRevision = TemporaryGitRepository.PushBehindCallersBack(
            origin,
            "main",
            "colleague.txt",
            "colleague content\n",
            "colleague commit");

        var beforeHead = HeadRevision(repository.RootPath);
        var beforeMain = LocalRef(repository.RootPath, "refs/heads/main");
        var beforeStatus = PorcelainStatus(repository.RootPath);
        var beforeWorktreeFiles = WorktreeFileHashes(repository.RootPath);

        var result = await CreateTracker().RefreshAsync(
            repository.RootPath,
            "refs/remotes/origin/main",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(newRevision, result.Data);
        Assert.Equal(newRevision, LocalRef(repository.RootPath, "refs/remotes/origin/main"));
        Assert.Equal(beforeHead, HeadRevision(repository.RootPath));
        Assert.Equal(beforeMain, LocalRef(repository.RootPath, "refs/heads/main"));
        Assert.Equal(beforeStatus, PorcelainStatus(repository.RootPath));
        Assert.Equal(beforeWorktreeFiles, WorktreeFileHashes(repository.RootPath));

        var checkResult = await CreateTracker().CheckAsync(
            repository.RootPath,
            "refs/remotes/origin/main",
            TestContext.Current.CancellationToken);
        Assert.True(checkResult.IsSuccess);
        Assert.Equal(RemoteBaselineState.Current, checkResult.Data!.State);
    }

    /// <summary>
    ///     Asynchronously reports no remote without attempting a network call for a repository with no configured
    ///     remotes.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CheckAsync_NoConfiguredRemote_ReturnsNoRemote()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateRemoteTrackingBranch("origin", "main");

        var result = await CreateTracker().CheckAsync(
            repository.RootPath,
            "refs/remotes/origin/main",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RemoteBaselineState.NoRemote, result.Data!.State);
        Assert.Null(result.Data.RemoteRevision);
    }

    /// <summary>
    ///     Asynchronously fails refresh for a repository with no configured remotes.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RefreshAsync_NoConfiguredRemote_FailsWithNoRemoteConfigured()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateRemoteTrackingBranch("origin", "main");

        var result = await CreateTracker().RefreshAsync(
            repository.RootPath,
            "refs/remotes/origin/main",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("comparison.noRemoteConfigured", error.Code);
    }

    /// <summary>
    ///     Asynchronously fails fast without hanging on a credential prompt when the configured remote points at a
    ///     nonexistent local path.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CheckAsync_UnreachableRemote_FailsFastWithoutPrompting()
    {
        using var repository = new TemporaryGitRepository();
        repository.AddUnreachableOrigin();

        var startedAt = Stopwatch.GetTimestamp();
        var result = await CreateTracker().CheckAsync(
            repository.RootPath,
            "refs/remotes/origin/main",
            TestContext.Current.CancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);

        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("comparison.remoteUnreachable", error.Code);
        Assert.True(
            elapsed < TimeSpan.FromSeconds(5),
            $"Expected a fast, prompt-free failure but the check took {elapsed}.");
    }

    private static GitRemoteBaselineTracker CreateTracker() =>
        new(
            new GitRepositoryInspector(
                new GitCliCommandRunner(),
                new PhysicalRepositoryPathResolver(),
                NullLogger<GitRepositoryInspector>.Instance),
            new GitCliCommandRunner(),
            NullLogger<GitRemoteBaselineTracker>.Instance);

    private static string HeadRevision(string repositoryPath) =>
        TemporaryGitRepository.RunGit(["-C", repositoryPath, "rev-parse", "--verify", "HEAD"]).StandardOutput.Trim();

    private static string LocalRef(string repositoryPath, string refName) =>
        TemporaryGitRepository.RunGit(
            ["-C", repositoryPath, "rev-parse", "--verify", refName]).StandardOutput.Trim();

    private static string PorcelainStatus(string repositoryPath)
    {
        var status = TemporaryGitRepository.RunGit(
            ["-C", repositoryPath, "status", "--porcelain=v1", "--untracked-files=all"]);
        return string.Join(
            "\n",
            status.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            status.StandardOutput,
            status.StandardError);
    }

    private static IReadOnlyDictionary<string, string> WorktreeFileHashes(string repositoryPath)
    {
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var filePath in Directory.EnumerateFiles(repositoryPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(repositoryPath, filePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            if (relativePath == ".git" || relativePath.StartsWith(".git/", StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = File.OpenRead(filePath);
            hashes[relativePath] = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
        }

        return hashes;
    }
}
