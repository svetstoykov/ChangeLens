using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Sdk;

namespace ChangeLens.Infrastructure.IntegrationTests.Comparisons;

/// <summary>
///     Verifies real Git comparison preparation semantics and read-only behavior.
/// </summary>
public sealed class GitComparisonPreparerIntegrationTests
{
    /// <summary>
    ///     Asynchronously prepares an empty equal-history comparison without changing repository state.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncEqualHistoryReturnsEmptyWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");

        var result = await PrepareWithoutMutationAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.TargetOnlyCommitCount);
        Assert.Equal(0, result.Data.CurrentWorkCommitCount);
        Assert.Equal(0, result.Data.Files.ChangedFileTotal);
        Assert.Equal(ComparisonReadiness.Empty, result.Data.Readiness);
        Assert.Equal(repository.Revision, result.Data.MergeBaseRevision);
    }

    /// <summary>
    ///     Asynchronously maps target-only and current-work counts for two-sided divergence.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncDivergedHistoryMapsIndependentGitCountsWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.CommitFile("current.txt", "current\n", "current side");
        repository.CheckoutBranch("topic");
        repository.CommitFile("target.txt", "target\n", "target side");
        repository.CheckoutBranch("main");

        var expected = TemporaryGitRepository.RunGit(
            [
                "-C",
                repository.RootPath,
                "rev-list",
                "--left-right",
                "--count",
                "refs/heads/topic...HEAD",
                "--",
            ]);
        Assert.Equal(0, expected.ExitCode);
        var expectedCounts = expected.StandardOutput.Trim().Split('\t');

        var result = await PrepareWithoutMutationAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        Assert.Equal(int.Parse(expectedCounts[0]), result.Data!.TargetOnlyCommitCount);
        Assert.Equal(int.Parse(expectedCounts[1]), result.Data.CurrentWorkCommitCount);
        Assert.Equal(1, result.Data.Files.ChangedFileTotal);
        Assert.Equal(ComparisonReadiness.Ready, result.Data.Readiness);
    }

    /// <summary>
    ///     Asynchronously distinguishes target-ahead and current-ahead one-sided histories.
    /// </summary>
    /// <param name="targetAhead">Whether the new commit belongs to the target side.</param>
    /// <param name="expectedTargetOnly">The expected target-only commit count.</param>
    /// <param name="expectedCurrentWork">The expected current-work commit count.</param>
    /// <param name="expectedChangedFiles">The expected changed-file total.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Theory]
    [InlineData(true, 1, 0, 0)]
    [InlineData(false, 0, 1, 1)]
    public async Task PrepareAsyncOneSidedHistoryMapsExpectedScopeWithoutMutation(
        bool targetAhead,
        int expectedTargetOnly,
        int expectedCurrentWork,
        int expectedChangedFiles)
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        if (targetAhead)
        {
            repository.CheckoutBranch("topic");
        }

        repository.CommitFile("side.txt", "side\n", "one side");
        if (targetAhead)
        {
            repository.CheckoutBranch("main");
        }

        var result = await PrepareWithoutMutationAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTargetOnly, result.Data!.TargetOnlyCommitCount);
        Assert.Equal(expectedCurrentWork, result.Data.CurrentWorkCommitCount);
        Assert.Equal(expectedChangedFiles, result.Data.Files.ChangedFileTotal);
    }

    /// <summary>
    ///     Asynchronously combines committed, unstaged, and untracked paths with spaces and Unicode.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncWorkingTreeAndUnicodePathsReturnsReadyAggregatesWithoutMutation()
    {
        using var repository = new TemporaryGitRepository("repository with spaces Ж");
        repository.CreateLocalBranch("тема/branch");
        repository.CommitFile("committed Ж.txt", "committed\n", "current commit");
        repository.WriteFile("committed Ж.txt", "modified\n");
        repository.WriteFile("untracked space.txt", "untracked\n");

        var result = await PrepareWithoutMutationAsync(
            repository,
            "refs/heads/тема/branch");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Files.ChangedFileTotal);
        Assert.Equal(2, result.Data.Files.UncommittedFileTotal);
        Assert.Equal(0, result.Data.Files.StagedFileCount);
        Assert.Equal(1, result.Data.Files.UnstagedFileCount);
        Assert.Equal(1, result.Data.Files.UntrackedFileCount);
        Assert.Equal(ComparisonReadiness.Ready, result.Data.Readiness);
    }

    /// <summary>
    ///     Asynchronously preserves overlapping staged, unstaged, untracked, and ignored category semantics.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncOverlappingWorkingTreeStatesReturnsDistinctCountsWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile(".gitignore", "ignored.txt\n", "add ignore rules");
        repository.CommitFile("unstaged.txt", "base\n", "add tracked file");
        repository.CreateLocalBranch("topic");
        repository.WriteFile("fixture.txt", "staged\n");
        repository.Stage("fixture.txt");
        repository.WriteFile("fixture.txt", "staged and unstaged\n");
        repository.WriteFile("unstaged.txt", "unstaged\n");
        repository.WriteFile("staged.txt", "staged\n");
        repository.Stage("staged.txt");
        repository.WriteFile("untracked.txt", "untracked\n");
        repository.WriteFile("ignored.txt", "ignored\n");

        var result = await PrepareWithoutMutationAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Data!.Files.ChangedFileTotal);
        Assert.Equal(4, result.Data.Files.UncommittedFileTotal);
        Assert.Equal(2, result.Data.Files.StagedFileCount);
        Assert.Equal(2, result.Data.Files.UnstagedFileCount);
        Assert.Equal(1, result.Data.Files.UntrackedFileCount);
        Assert.Equal(0, result.Data.Files.ConflictedFileCount);
    }

    /// <summary>
    ///     Asynchronously parses live rename, delete, type-change, symbolic-link, and submodule status facts.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncReviewedGitFileKindsReturnsDistinctCountsWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("rename-source.txt", "rename\n", "add rename source");
        repository.CommitFile("delete-source.txt", "delete\n", "add delete source");
        repository.CommitFile("type-source.txt", "type\n", "add type source");
        var submodulePath = repository.CreateSubmodule();
        repository.CreateLocalBranch("topic");
        repository.Move("rename-source.txt", "rename-target.txt");
        repository.Remove("delete-source.txt");
        repository.ReplaceWithSymbolicLink("type-source.txt", "fixture.txt");
        File.WriteAllText(
            Path.Combine(submodulePath, "submodule.txt"),
            "dirty submodule content\n");

        var result = await PrepareWithoutMutationAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Data!.Files.ChangedFileTotal);
        Assert.Equal(4, result.Data.Files.UncommittedFileTotal);
        Assert.Equal(3, result.Data.Files.StagedFileCount);
        Assert.Equal(1, result.Data.Files.UnstagedFileCount);
        Assert.Equal(0, result.Data.Files.UntrackedFileCount);
        Assert.Equal(0, result.Data.Files.ConflictedFileCount);
        Assert.Equal(ComparisonReadiness.Ready, result.Data.Readiness);
    }

    /// <summary>
    ///     Asynchronously returns conflict readiness while preserving aggregate file counts.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncUnmergedWorkingTreeReturnsConflictReadinessWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.CommitFile("fixture.txt", "current\n", "current edit");
        repository.CheckoutBranch("topic");
        repository.CommitFile("fixture.txt", "target\n", "target edit");
        repository.CheckoutBranch("main");
        repository.MergeExpectingConflict("topic");

        var result = await PrepareWithoutMutationAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonReadiness.Conflicts, result.Data!.Readiness);
        Assert.Equal(1, result.Data.Files.ChangedFileTotal);
        Assert.Equal(1, result.Data.Files.UncommittedFileTotal);
        Assert.Equal(1, result.Data.Files.ConflictedFileCount);
    }

    /// <summary>
    ///     Asynchronously reports an unrelated target history through the documented domain error.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncUnrelatedHistoryReturnsStableFailureWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateUnrelatedBranch("unrelated");

        var result = await PrepareWithoutMutationAsync(
            repository,
            "refs/heads/unrelated");

        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("comparison.unrelatedHistory", error.Code);
    }

    /// <summary>
    ///     Asynchronously rejects a real symbolic cached remote HEAD as a comparison target.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncSymbolicRemoteHeadReturnsTargetInvalidWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateRemoteTrackingBranch("origin", "main");
        repository.CreateSymbolicRemoteHead("origin", "main");

        var result = await PrepareWithoutMutationAsync(
            repository,
            "refs/remotes/origin/HEAD");

        Assert.True(result.IsFailure);
        Assert.Equal("comparison.targetInvalid", Assert.Single(result.Errors).Code);
    }

    /// <summary>
    ///     Asynchronously reports criss-cross histories with multiple best merge bases.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncCrissCrossHistoryReturnsAmbiguousBaseWithoutMutation()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateCrissCrossHistory("topic");

        var result = await PrepareWithoutMutationAsync(repository, "refs/heads/topic");

        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("comparison.ambiguousBase", error.Code);
    }

    /// <summary>
    ///     Asynchronously reports a target deleted immediately after its discovery as unavailable without retrying.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncTargetDeletedAfterDiscoveryReturnsUnavailableWithoutRetry()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        var deleted = false;
        var runner = new HookedGitCommandRunner(
            new GitCliCommandRunner(),
            (command, _, _) =>
            {
                if (!deleted &&
                    command.Arguments.Contains("for-each-ref", StringComparer.Ordinal))
                {
                    var deletion = TemporaryGitRepository.RunGit(
                        [
                            "-C",
                            repository.RootPath,
                            "update-ref",
                            "-d",
                            "refs/heads/topic",
                        ]);
                    Assert.Equal(0, deletion.ExitCode);
                    deleted = true;
                }

                return Task.CompletedTask;
            });
        var preparer = CreatePreparer(runner);

        var result = await preparer.PrepareAsync(
            repository.RootPath,
            "refs/heads/topic",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("comparison.targetUnavailable", Assert.Single(result.Errors).Code);
        Assert.True(deleted);
        Assert.Equal(9, runner.CompletedCommandCount);
    }

    /// <summary>
    ///     Asynchronously detects a working-tree change made after the beginning status without retrying.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncWorkingTreeChangesDuringPreparationReturnsConflictWithoutRetry()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        var changed = false;
        var runner = new HookedGitCommandRunner(
            new GitCliCommandRunner(),
            (command, _, _) =>
            {
                if (!changed &&
                    command.Arguments.Contains("status", StringComparer.Ordinal))
                {
                    repository.WriteFile("changed-during-preparation.txt", "changed\n");
                    changed = true;
                }

                return Task.CompletedTask;
            });
        var preparer = CreatePreparer(runner);

        var result = await preparer.PrepareAsync(
            repository.RootPath,
            "refs/heads/topic",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "comparison.changedDuringPreparation",
            Assert.Single(result.Errors).Code);
        Assert.True(changed);
        Assert.Equal(18, runner.CompletedCommandCount);
    }

    /// <summary>
    ///     Asynchronously detects a branch switch made after committed facts are read.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncBranchChangesDuringPreparationReturnsConflictWithoutRetry()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.CreateLocalBranch("other");
        var changed = false;
        var runner = new HookedGitCommandRunner(
            new GitCliCommandRunner(),
            (command, _, _) =>
            {
                if (!changed &&
                    command.Arguments.Contains("diff", StringComparer.Ordinal))
                {
                    repository.CheckoutBranch("other");
                    changed = true;
                }

                return Task.CompletedTask;
            });
        var preparer = CreatePreparer(runner);

        var result = await preparer.PrepareAsync(
            repository.RootPath,
            "refs/heads/topic",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "comparison.changedDuringPreparation",
            Assert.Single(result.Errors).Code);
        Assert.True(changed);
        Assert.Equal(18, runner.CompletedCommandCount);
    }

    /// <summary>
    ///     Asynchronously detects a selected target move made after committed facts are read.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncTargetChangesDuringPreparationReturnsConflictWithoutRetry()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        repository.CommitFile("current.txt", "current\n", "current change");
        var changed = false;
        var runner = new HookedGitCommandRunner(
            new GitCliCommandRunner(),
            (command, _, _) =>
            {
                if (!changed &&
                    command.Arguments.Contains("diff", StringComparer.Ordinal))
                {
                    var update = TemporaryGitRepository.RunGit(
                        [
                            "-C",
                            repository.RootPath,
                            "update-ref",
                            "refs/heads/topic",
                            repository.Revision,
                        ]);
                    Assert.Equal(0, update.ExitCode);
                    changed = true;
                }

                return Task.CompletedTask;
            });
        var preparer = CreatePreparer(runner);

        var result = await preparer.PrepareAsync(
            repository.RootPath,
            "refs/heads/topic",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "comparison.changedDuringPreparation",
            Assert.Single(result.Errors).Code);
        Assert.True(changed);
        Assert.Equal(18, runner.CompletedCommandCount);
    }

    /// <summary>
    ///     Asynchronously recognizes a target deleted before the ending resolution as confirmed drift.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncTargetDeletedBeforeEndingResolutionReturnsConflictWithoutRetry()
    {
        using var repository = new TemporaryGitRepository();
        repository.CreateLocalBranch("topic");
        var deleted = false;
        var runner = new HookedGitCommandRunner(
            new GitCliCommandRunner(),
            (command, _, _) =>
            {
                if (!deleted &&
                    command.Arguments.Contains("diff", StringComparer.Ordinal))
                {
                    var deletion = TemporaryGitRepository.RunGit(
                        [
                            "-C",
                            repository.RootPath,
                            "update-ref",
                            "-d",
                            "refs/heads/topic",
                        ]);
                    Assert.Equal(0, deletion.ExitCode);
                    deleted = true;
                }

                return Task.CompletedTask;
            });
        var preparer = CreatePreparer(runner);

        var result = await preparer.PrepareAsync(
            repository.RootPath,
            "refs/heads/topic",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "comparison.changedDuringPreparation",
            Assert.Single(result.Errors).Code);
        Assert.True(deleted);
        Assert.Equal(18, runner.CompletedCommandCount);
    }

    /// <summary>
    ///     Asynchronously prepares a full SHA-256 comparison when the installed Git supports it.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PrepareAsyncSha256RepositoryReturnsFullRevisionsWithoutMutation()
    {
        if (!TemporaryGitRepository.SupportsObjectFormat("sha256"))
        {
            throw SkipException.ForSkip("The installed Git executable does not support SHA-256 repositories.");
        }

        using var repository = new TemporaryGitRepository("sha256 comparison", "sha256");
        repository.CreateLocalBranch("topic");

        var result = await PrepareWithoutMutationAsync(repository, "refs/heads/topic");

        Assert.True(result.IsSuccess);
        Assert.Equal(64, result.Data!.Repository.Head.Revision.Length);
        Assert.Equal(64, result.Data.Target.Revision.Length);
        Assert.Equal(64, result.Data.MergeBaseRevision.Length);
    }

    private static async Task<ChangeLens.Core.Results.Models.Result<PreparedComparison>>
        PrepareWithoutMutationAsync(
            TemporaryGitRepository repository,
            string target)
    {
        var before = RepositoryStateSnapshot.Capture(repository.RootPath);
        var runner = new GitCliCommandRunner();
        var preparer = CreatePreparer(runner);

        var result = await preparer.PrepareAsync(
            repository.RootPath,
            target,
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
}
