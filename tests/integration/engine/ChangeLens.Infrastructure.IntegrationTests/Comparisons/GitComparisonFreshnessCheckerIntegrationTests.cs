using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Comparisons;

/// <summary>
///     Verifies real Git comparison freshness semantics and read-only behavior.
/// </summary>
public sealed class GitComparisonFreshnessCheckerIntegrationTests
{
    /// <summary>
    ///     Returns current for unchanged repository facts without mutating the repository.
    /// </summary>
    [Fact]
    public async Task CheckAsyncUnchangedRepositoryReturnsCurrentWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        var prepared = await PrepareAsync(repository);

        var result = await CheckWithoutMutationAsync(repository, prepared);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Current, result.Data!.State);
    }

    /// <summary>
    ///     Returns stale for external edits, staging changes, and untracked additions.
    /// </summary>
    /// <param name="change">The repository change applied after preparation.</param>
    [Theory]
    [InlineData("edit")]
    [InlineData("stage")]
    [InlineData("untracked")]
    public async Task CheckAsyncWorkingTreeChangesReturnStaleWithoutMutation(string change)
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        var prepared = await PrepareAsync(repository);
        switch (change)
        {
            case "edit":
                repository.WriteFile("fixture.txt", "external edit\n");
                break;
            case "stage":
                repository.WriteFile("fixture.txt", "staged edit\n");
                repository.Stage("fixture.txt");
                break;
            case "untracked":
                repository.WriteFile("untracked.txt", "untracked\n");
                break;
        }

        var result = await CheckWithoutMutationAsync(repository, prepared);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data!.State);
    }

    /// <summary>
    ///     Returns stale when the checked-out branch changes at the same revision.
    /// </summary>
    [Fact]
    public async Task CheckAsyncBranchSwitchReturnsStaleWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        var prepared = await PrepareAsync(repository);
        repository.CheckoutBranch("topic");

        var result = await CheckWithoutMutationAsync(repository, prepared);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data!.State);
    }

    /// <summary>
    ///     Returns stale when the selected target moves or is deleted.
    /// </summary>
    /// <param name="deleteTarget">Whether to delete the target instead of moving it.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckAsyncTargetChangeReturnsStaleWithoutMutation(bool deleteTarget)
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        var prepared = await PrepareAsync(repository);
        if (deleteTarget)
        {
            var deletion = TemporaryGitRepository.RunGit(
                ["-C", repository.RootPath, "update-ref", "-d", "refs/heads/topic"]);
            Assert.Equal(0, deletion.ExitCode);
        }
        else
        {
            repository.CheckoutBranch("topic");
            repository.CommitFile("target.txt", "target\n", "move target");
            repository.CheckoutBranch("main");
        }

        var result = await CheckWithoutMutationAsync(repository, prepared);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data!.State);
    }

    /// <summary>
    ///     Keeps a changed file current when its normalized status facts remain the same.
    /// </summary>
    [Fact]
    public async Task CheckAsyncSameStatusContentEditReturnsCurrentWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.WriteFile("fixture.txt", "first edit\n");
        var prepared = await PrepareAsync(repository);
        repository.WriteFile("fixture.txt", "second edit\n");

        var result = await CheckWithoutMutationAsync(repository, prepared);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Current, result.Data!.State);
    }

    /// <summary>
    ///     Returns stale when a staged prepared change is unstaged without mutating the repository further.
    /// </summary>
    [Fact]
    public async Task CheckAsyncUnstageReturnsStaleWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.WriteFile("fixture.txt", "staged edit\n");
        repository.Stage("fixture.txt");
        var prepared = await PrepareAsync(repository);
        var unstage = TemporaryGitRepository.RunGit(
            ["-C", repository.RootPath, "restore", "--staged", "fixture.txt"]);
        Assert.Equal(0, unstage.ExitCode);

        var result = await CheckWithoutMutationAsync(repository, prepared);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data!.State);
    }

    private static async Task<PreparedComparison> PrepareAsync(TemporaryGitRepository repository)
    {
        var preparer = CreatePreparer(new GitCliCommandRunner());
        var result = await preparer.PrepareAsync(
            repository.RootPath,
            "refs/heads/topic",
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        return result.Data!;
    }

    private static async Task<ChangeLens.Core.Results.Models.Result<ComparisonFreshnessCheck>>
        CheckWithoutMutationAsync(
            TemporaryGitRepository repository,
            PreparedComparison prepared)
    {
        var before = RepositoryStateSnapshot.Capture(repository.RootPath);
        var runner = new GitCliCommandRunner();
        var checker = CreateChecker(runner);

        var result = await checker.CheckAsync(
            repository.RootPath,
            prepared.Target.FullName,
            prepared.FreshnessToken,
            TestContext.Current.CancellationToken);

        var after = RepositoryStateSnapshot.Capture(repository.RootPath);
        Assert.Equal(before.PorcelainStatus, after.PorcelainStatus);
        Assert.Equal(before.FileHashes, after.FileHashes);
        return result;
    }

    private static GitComparisonPreparer CreatePreparer(
        ChangeLens.Core.Git.Interfaces.IGitCommandRunner runner)
    {
        var inspector = new ChangeLens.Core.Git.Services.GitRepositoryInspector(
            runner,
            new PhysicalRepositoryPathResolver(),
            NullLogger<ChangeLens.Core.Git.Services.GitRepositoryInspector>.Instance);
        return new GitComparisonPreparer(
            inspector,
            new GitComparisonTargetDiscovery(inspector, runner, NullLogger<GitComparisonTargetDiscovery>.Instance),
            runner,
            new ComparisonFileSummaryComposer(),
            NullLogger<GitComparisonPreparer>.Instance);
    }

    private static GitComparisonFreshnessChecker CreateChecker(
        ChangeLens.Core.Git.Interfaces.IGitCommandRunner runner)
    {
        var inspector = new ChangeLens.Core.Git.Services.GitRepositoryInspector(
            runner,
            new PhysicalRepositoryPathResolver(),
            NullLogger<ChangeLens.Core.Git.Services.GitRepositoryInspector>.Instance);
        return new GitComparisonFreshnessChecker(
            inspector,
            new GitComparisonTargetDiscovery(inspector, runner, NullLogger<GitComparisonTargetDiscovery>.Instance),
            runner,
            NullLogger<GitComparisonFreshnessChecker>.Instance);
    }
}
