using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Git.Services;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Xunit;
using Xunit.Sdk;

namespace ChangeLens.Infrastructure.IntegrationTests.Comparisons;

/// <summary>
///     Verifies real Git target discovery uses only cached refs and preserves repository state.
/// </summary>
public sealed class GitComparisonTargetDiscoveryIntegrationTests
{
    /// <summary>
    ///     Asynchronously discovers local and one remote's cached refs and uses origin's recorded default.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ListAsync_OneRemote_ReturnsCachedTargetsAndOriginDefaultWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.CreateRemoteTrackingBranch("origin", "main");
        repository.CreateRemoteTrackingBranch("origin", "topic");
        repository.CreateSymbolicRemoteHead("origin", "main");

        var result = await ListWithoutMutationAsync(repository);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["refs/heads/topic", "refs/remotes/origin/main", "refs/remotes/origin/topic"],
            result.Data!.Targets.Select(target => target.FullName));
        Assert.Equal(
            "refs/remotes/origin/main",
            result.Data.SuggestedTarget!.FullName);
    }

    /// <summary>
    ///     Asynchronously prefers the current branch's configured remote default among multiple cached remotes.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ListAsync_MultipleRemotes_PrefersConfiguredRemoteDefaultWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateRemoteTrackingBranch("origin", "main");
        repository.CreateSymbolicRemoteHead("origin", "main");
        repository.CreateRemoteTrackingBranch("upstream", "trunk");
        repository.CreateSymbolicRemoteHead("upstream", "trunk");
        repository.ConfigureBranchUpstream("main", "upstream", "trunk");

        var result = await ListWithoutMutationAsync(repository);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "refs/remotes/upstream/trunk",
            result.Data!.SuggestedTarget!.FullName);
        Assert.Equal(
            ["refs/remotes/origin/main", "refs/remotes/upstream/trunk"],
            result.Data.Targets.Select(target => target.FullName));
    }

    /// <summary>
    ///     Asynchronously handles spaces in the repository path and Unicode ref names without guessing a default.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ListAsync_UnicodeRefsAndSpacedPath_PreservesExactNamesWithoutMutation()
    {
        using var repository = new TemporaryGitRepository("repository with spaces Ж");
        repository.CreateLocalBranch("функция");
        repository.CreateRemoteTrackingBranch("origin", "тема");

        var result = await ListWithoutMutationAsync(repository);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["функция", "origin/тема"],
            result.Data!.Targets.Select(target => target.Name));
        Assert.Null(result.Data.SuggestedTarget);
    }

    /// <summary>
    ///     Asynchronously discovers a full SHA-256 cached ref when the installed Git supports it.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ListAsync_Sha256Repository_ReturnsFullRevisionWithoutMutation()
    {
        if (!TemporaryGitRepository.SupportsObjectFormat("sha256"))
        {
            throw SkipException.ForSkip("The installed Git executable does not support SHA-256 repositories.");
        }

        using var repository = new TemporaryGitRepository("sha256 targets", "sha256");
        repository.CreateLocalBranch("topic");

        var result = await ListWithoutMutationAsync(repository);

        Assert.True(result.IsSuccess);
        Assert.Equal(64, Assert.Single(result.Data!.Targets).Revision.Length);
    }

    /// <summary>
    ///     Asynchronously succeeds when detached HEAD and symbolic metadata leave no usable target.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ListAsync_NoUsableTarget_ReturnsEmptySetWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateRemoteTrackingBranch("origin", "main");
        repository.CreateSymbolicRemoteHead("origin", "missing");
        TemporaryGitRepository.RunGit(
            ["-C", repository.RootPath, "update-ref", "-d", "refs/remotes/origin/main"]);

        var result = await ListWithoutMutationAsync(repository);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Targets);
        Assert.Null(result.Data.SuggestedTarget);
    }

    private static async Task<ChangeLens.Core.Results.Models.Result<ComparisonTargetSet>>
        ListWithoutMutationAsync(TemporaryGitRepository repository)
    {
        var before = RepositoryStateSnapshot.Capture(repository.RootPath);
        var runner = new GitCliCommandRunner();
        var discovery = new GitComparisonTargetDiscovery(
            new GitRepositoryInspector(
                runner,
                new PhysicalRepositoryPathResolver()),
            runner);

        var result = await discovery.ListAsync(
            repository.RootPath,
            null,
            null,
            null,
            TestContext.Current.CancellationToken);

        var after = RepositoryStateSnapshot.Capture(repository.RootPath);
        Assert.Equal(before.PorcelainStatus, after.PorcelainStatus);
        Assert.Equal(before.FileHashes, after.FileHashes);
        return result;
    }
}
