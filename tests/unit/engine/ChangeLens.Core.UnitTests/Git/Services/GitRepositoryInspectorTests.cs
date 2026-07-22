using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.UnitTests.Git.Support;
using Xunit;

namespace ChangeLens.Core.UnitTests.Git.Services;

/// <summary>
///     Verifies deterministic composition of Git repository inspection facts.
/// </summary>
public sealed class GitRepositoryInspectorTests
{
    private const string Sha1Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string Sha256Revision =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    ///     Verifies that structurally invalid repository paths are rejected before path resolution or Git execution.
    /// </summary>
    /// <param name="path">The invalid repository path.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("/repository\0child")]
    public async Task InspectAsyncRejectsInvalidPathBeforeExternalCalls(string? path)
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync(path, TestContext.Current.CancellationToken);

        AssertInvalidPath(result);
        Assert.Empty(resolver.Paths);
        Assert.Empty(runner.Commands);
    }

    /// <summary>
    ///     Verifies that an unpaired UTF-16 surrogate is rejected before path resolution or Git execution.
    /// </summary>
    [Fact]
    public async Task InspectAsyncRejectsInvalidUtf16BeforeExternalCalls()
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var inspector = new GitRepositoryInspector(runner, resolver);
        var path = new string((char)0xD800, 1);

        var result = await inspector.InspectAsync(path, TestContext.Current.CancellationToken);

        AssertInvalidPath(result);
        Assert.Empty(resolver.Paths);
        Assert.Empty(runner.Commands);
    }

    /// <summary>
    ///     Verifies that repository paths longer than 8,192 Unicode scalars are rejected.
    /// </summary>
    [Fact]
    public async Task InspectAsyncRejectsPathLongerThan8192Scalars()
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var inspector = new GitRepositoryInspector(runner, resolver);
        var path = string.Concat(Enumerable.Repeat("😀", 8_193));

        var result = await inspector.InspectAsync(path, TestContext.Current.CancellationToken);

        AssertInvalidPath(result);
        Assert.Empty(resolver.Paths);
        Assert.Empty(runner.Commands);
    }

    /// <summary>
    ///     Verifies that a valid 8,192-scalar path reaches the path resolver.
    /// </summary>
    [Fact]
    public async Task InspectAsyncAcceptsPathWith8192Scalars()
    {
        var error = OperationError.NotFound("missing", RepositoryErrorCode.PathNotFound);
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Fail<string>(error));
        var inspector = new GitRepositoryInspector(runner, resolver);
        var path = string.Concat(Enumerable.Repeat("😀", 8_192));

        var result = await inspector.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Same(error, Assert.Single(result.Errors));
        Assert.Equal([path], resolver.Paths);
        Assert.Empty(runner.Commands);
    }

    /// <summary>
    ///     Verifies that initial canonicalization failures are forwarded by reference before Git runs.
    /// </summary>
    [Fact]
    public async Task InspectAsyncForwardsSelectionResolutionFailureBeforeGit()
    {
        var error = OperationError.Unauthorized("denied", RepositoryErrorCode.AccessDenied);
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Fail<string>(error));
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested/subdirectory", TestContext.Current.CancellationToken);

        Assert.Same(error, Assert.Single(result.Errors));
        Assert.Equal(["/requested/subdirectory"], resolver.Paths);
        Assert.Empty(runner.Commands);
    }

    /// <summary>
    ///     Verifies the exact Git fact sequence, roots, resource bounds, and branch result.
    /// </summary>
    [Fact]
    public async Task InspectAsyncExecutesExactFactSequenceWithinOneBudget()
    {
        var runner = new StubGitCommandRunner();
        EnqueueSuccessfulGitInspection(runner, "/reported/root", Sha1Revision, "feature/open-safely");
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Success<string>("/physical/selection"));
        resolver.Enqueue(Result.Success<string>("/physical/root"));
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested/subdirectory", TestContext.Current.CancellationToken);

        var repository = Assert.IsType<RepositoryDescriptor>(result.Data);
        Assert.Equal("root", repository.Name);
        Assert.Equal("/physical/root", repository.CanonicalPath);
        var head = Assert.IsType<BranchRepositoryHead>(repository.Head);
        Assert.Equal("feature/open-safely", head.Name);
        Assert.Equal(Sha1Revision, head.Revision);
        Assert.Equal(["/requested/subdirectory", "/reported/root"], resolver.Paths);
        Assert.Collection(
            runner.Commands,
            command => Assert.Equal(["--version"], command.Arguments),
            command => Assert.Equal(
                ["-C", "/physical/selection", "rev-parse", "--is-inside-work-tree"],
                command.Arguments),
            command => Assert.Equal(
                ["-C", "/physical/selection", "rev-parse", "--is-bare-repository"],
                command.Arguments),
            command => Assert.Equal(
                ["-C", "/physical/selection", "rev-parse", "--show-toplevel"],
                command.Arguments),
            command => Assert.Equal(
                ["-C", "/physical/root", "rev-parse", "--verify", "HEAD^{commit}"],
                command.Arguments),
            command => Assert.Equal(
                ["-C", "/physical/root", "symbolic-ref", "--quiet", "--short", "HEAD"],
                command.Arguments));
        Assert.All(
            runner.Commands,
            command =>
            {
                Assert.Equal(65_536, command.MaximumStreamBytes);
                Assert.True(command.Timeout > TimeSpan.Zero);
                Assert.True(command.Timeout <= TimeSpan.FromSeconds(15));
            });
        Assert.True(
            runner.Commands.Zip(runner.Commands.Skip(1)).All(pair => pair.First.Timeout >= pair.Second.Timeout));
    }

    /// <summary>
    ///     Verifies that elapsed time is deducted from the timeout assigned to later Git commands.
    /// </summary>
    [Fact]
    public async Task InspectAsyncDeductsElapsedTimeFromLaterCommandTimeouts()
    {
        var runner = new StubGitCommandRunner();
        runner.Enqueue(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            return Output("git version 2.51.0\n");
        });
        runner.Enqueue(Output("true\n"));
        runner.Enqueue(Output("false\n"));
        runner.Enqueue(Output("/reported/root\n"));
        runner.Enqueue(Output(Sha1Revision + "\n"));
        runner.Enqueue(Output("main\n"));
        var resolver = RootResolver("/physical/root");
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(runner.Commands[0].Timeout - runner.Commands[1].Timeout >= TimeSpan.FromMilliseconds(50));
    }

    /// <summary>
    ///     Verifies that Git availability is checked again when inspection is retried.
    /// </summary>
    [Fact]
    public async Task InspectAsyncRunsVersionOnEveryInspection()
    {
        var runner = new StubGitCommandRunner();
        EnqueueSuccessfulGitInspection(runner, "/reported/root", Sha1Revision, "main");
        EnqueueSuccessfulGitInspection(runner, "/reported/root", Sha1Revision, "main");
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Success<string>("/physical/selection"));
        resolver.Enqueue(Result.Success<string>("/physical/root"));
        resolver.Enqueue(Result.Success<string>("/physical/selection"));
        resolver.Enqueue(Result.Success<string>("/physical/root"));
        var inspector = new GitRepositoryInspector(runner, resolver);

        var first = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);
        var second = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, runner.Commands.Count(command => command.Arguments.SequenceEqual(["--version"])));
        Assert.Equal(12, runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies that bare repositories take precedence when both repository facts are false for a working tree.
    /// </summary>
    [Fact]
    public async Task InspectAsyncReturnsBareBeforeNotRepository()
    {
        var runner = new StubGitCommandRunner();
        runner.Enqueue(Output("git version 2.51.0\n"));
        runner.Enqueue(Output("false\n"));
        runner.Enqueue(Output("true\n"));
        var resolver = SelectionResolver();
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.UnprocessableInput, error.Type);
        Assert.Equal(RepositoryErrorCode.WorkTreeUnavailable, error.Code);
        Assert.Equal(3, runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies that a non-working-tree selection is reported after both repository facts are read.
    /// </summary>
    [Fact]
    public async Task InspectAsyncReturnsNotRepositoryAfterBothBooleanFacts()
    {
        var runner = new StubGitCommandRunner();
        runner.Enqueue(Output("git version 2.51.0\n"));
        runner.Enqueue(Output("false\n"));
        runner.Enqueue(Output("false\n"));
        var resolver = SelectionResolver();
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.UnprocessableInput, error.Type);
        Assert.Equal(RepositoryErrorCode.NotGitRepository, error.Code);
        Assert.Equal(3, runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies that a repository without a committed HEAD returns the stable unborn error.
    /// </summary>
    [Fact]
    public async Task InspectAsyncReturnsHeadUnavailableForUnbornRepository()
    {
        var runner = new StubGitCommandRunner();
        runner.Enqueue(Output("git version 2.51.0\n"));
        runner.Enqueue(Output("true\n"));
        runner.Enqueue(Output("false\n"));
        runner.Enqueue(Output("/reported/root\n"));
        runner.Enqueue(new GitCommandOutput(128, string.Empty, "fatal: Needed a single revision"));
        var resolver = RootResolver("/physical/root");
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.UnprocessableInput, error.Type);
        Assert.Equal(RepositoryErrorCode.HeadUnavailable, error.Code);
        Assert.Equal(5, runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies that a linked worktree root and detached SHA-256 HEAD are preserved.
    /// </summary>
    [Fact]
    public async Task InspectAsyncReturnsDetachedSha256AtCanonicalLinkedWorktreeRoot()
    {
        var runner = new StubGitCommandRunner();
        EnqueueSuccessfulGitInspection(runner, "/reported/linked-worktree", Sha256Revision, null);
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Success<string>("/physical/linked-worktree/subdirectory"));
        resolver.Enqueue(Result.Success<string>("/physical/linked-worktree"));
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/selected/link", TestContext.Current.CancellationToken);

        var repository = Assert.IsType<RepositoryDescriptor>(result.Data);
        Assert.Equal("linked-worktree", repository.Name);
        Assert.Equal("/physical/linked-worktree", repository.CanonicalPath);
        var head = Assert.IsType<DetachedRepositoryHead>(repository.Head);
        Assert.Equal(Sha256Revision, head.Revision);
        Assert.Equal(["/selected/link", "/reported/linked-worktree"], resolver.Paths);
        Assert.Equal("/physical/linked-worktree/subdirectory", runner.Commands[1].Arguments[1]);
        Assert.Equal("/physical/linked-worktree", runner.Commands[4].Arguments[1]);
        Assert.Equal("/physical/linked-worktree", runner.Commands[5].Arguments[1]);
    }

    /// <summary>
    ///     Verifies that an unnamed filesystem root falls back to the canonical root string for display.
    /// </summary>
    [Fact]
    public async Task InspectAsyncUsesCanonicalRootAsNameWhenFinalComponentIsEmpty()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(Path.DirectorySeparatorChar.ToString()))!;
        var runner = new StubGitCommandRunner();
        EnqueueSuccessfulGitInspection(runner, root, Sha1Revision, "main");
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Success<string>(root));
        resolver.Enqueue(Result.Success<string>(root));
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync(root, TestContext.Current.CancellationToken);

        var repository = Assert.IsType<RepositoryDescriptor>(result.Data);
        Assert.Equal(root, repository.Name);
        Assert.Equal(root, repository.CanonicalPath);
    }

    /// <summary>
    ///     Verifies that top-level canonicalization failures are forwarded by reference before HEAD is inspected.
    /// </summary>
    [Fact]
    public async Task InspectAsyncForwardsRootResolutionFailureBeforeHead()
    {
        var error = OperationError.Unauthorized("denied", RepositoryErrorCode.AccessDenied);
        var runner = new StubGitCommandRunner();
        runner.Enqueue(Output("git version 2.51.0\n"));
        runner.Enqueue(Output("true\n"));
        runner.Enqueue(Output("false\n"));
        runner.Enqueue(Output("/reported/root\n"));
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Success<string>("/physical/selection"));
        resolver.Enqueue(Result.Fail<string>(error));
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);

        Assert.Same(error, Assert.Single(result.Errors));
        Assert.Equal(4, runner.Commands.Count);
        Assert.Equal(["/requested", "/reported/root"], resolver.Paths);
    }

    /// <summary>
    ///     Verifies that runner failures at every fact stage preserve error identity and stop later commands.
    /// </summary>
    /// <param name="failureIndex">The zero-based Git command index that fails.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task InspectAsyncForwardsRunnerFailureAndStopsLaterCommands(int failureIndex)
    {
        var error = OperationError.ExternalDependencyFailure("runner failure", "git.testFailure");
        var runner = new StubGitCommandRunner();
        EnqueueFailureAt(runner, failureIndex, error);
        var resolver = RootResolver("/physical/root");
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);

        Assert.Same(error, Assert.Single(result.Errors));
        Assert.Equal(failureIndex + 1, runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies that malformed Git output stops the workflow at the parser for that fact.
    /// </summary>
    /// <param name="failureIndex">The zero-based Git command index whose output is malformed.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task InspectAsyncStopsAfterMalformedGitFact(int failureIndex)
    {
        var runner = new StubGitCommandRunner();
        EnqueueMalformedAt(runner, failureIndex);
        var resolver = RootResolver("/physical/root");
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);

        Assert.Equal(RepositoryErrorCode.InspectionFailed, Assert.Single(result.Errors).Code);
        Assert.Equal(failureIndex + 1, runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies that a worktree-detection Git failure is safely classified.
    /// </summary>
    [Fact]
    public async Task InspectAsyncClassifiesFailedWorktreeDetection()
    {
        var runner = new StubGitCommandRunner();
        runner.Enqueue(Output("git version 2.51.0\n"));
        runner.Enqueue(new GitCommandOutput(128, string.Empty, "fatal: not a git repository"));
        var resolver = SelectionResolver();
        var inspector = new GitRepositoryInspector(runner, resolver);

        var result = await inspector.InspectAsync("/requested", TestContext.Current.CancellationToken);

        Assert.Equal(RepositoryErrorCode.NotGitRepository, Assert.Single(result.Errors).Code);
        Assert.Equal(2, runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies that cancellation requested by the caller remains exception-based.
    /// </summary>
    [Fact]
    public async Task InspectAsyncPropagatesCallerCancellation()
    {
        var enteredResolver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(async (_, cancellationToken) =>
        {
            enteredResolver.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Result.Success<string>("/unreachable");
        });
        var inspector = new GitRepositoryInspector(runner, resolver);
        using var cancellation = new CancellationTokenSource();

        var inspection = inspector.InspectAsync("/requested", cancellation.Token);
        await enteredResolver.Task;
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inspection);
        Assert.Empty(runner.Commands);
    }

    /// <summary>
    ///     Verifies that caller cancellation remains exception-based during top-level-root canonicalization.
    /// </summary>
    [Fact]
    public async Task InspectAsyncPropagatesCallerCancellationDuringRootResolution()
    {
        var enteredResolver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new StubGitCommandRunner();
        var successfulOutputs = SuccessfulOutputs();
        for (var index = 0; index < 4; index++)
        {
            runner.Enqueue(successfulOutputs[index]);
        }

        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Success<string>("/physical/selection"));
        resolver.Enqueue(async (_, cancellationToken) =>
        {
            enteredResolver.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Result.Success<string>("/unreachable");
        });
        var inspector = new GitRepositoryInspector(runner, resolver);
        using var cancellation = new CancellationTokenSource();

        var inspection = inspector.InspectAsync("/requested", cancellation.Token);
        await enteredResolver.Task;
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inspection);
        Assert.Equal(4, runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies that expiry during path resolution or any Git fact returns the stable timeout error.
    /// </summary>
    [Fact]
    public async Task InspectAsyncReturnsTimeoutWhenAnyActiveExternalCallExceedsSharedDeadline()
    {
        var inspections = Enumerable.Range(-2, 8).Select(StartTimeoutInspection).ToArray();

        var results = await Task.WhenAll(inspections);

        Assert.All(
            results,
            result =>
            {
                var error = Assert.Single(result.Errors);
                Assert.Equal(ErrorType.Timeout, error.Type);
                Assert.Equal(GitErrorCode.TimedOut, error.Code);
                Assert.Equal("Git repository inspection exceeded its allowed time.", error.Message);
            });
    }

    private static StubRepositoryPathResolver SelectionResolver()
    {
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Success<string>("/physical/selection"));
        return resolver;
    }

    private static StubRepositoryPathResolver RootResolver(string root)
    {
        var resolver = SelectionResolver();
        resolver.Enqueue(Result.Success<string>(root));
        return resolver;
    }

    private static Result<GitCommandOutput> Output(
        string standardOutput,
        int exitCode = 0,
        string standardError = "") =>
        Result.Success(new GitCommandOutput(exitCode, standardOutput, standardError));

    private static void EnqueueSuccessfulGitInspection(
        StubGitCommandRunner runner,
        string reportedRoot,
        string revision,
        string? branchName)
    {
        runner.Enqueue(Output("git version 2.51.0\n"));
        runner.Enqueue(Output("true\n"));
        runner.Enqueue(Output("false\n"));
        runner.Enqueue(Output(reportedRoot + "\n"));
        runner.Enqueue(Output(revision + "\n"));
        runner.Enqueue(branchName is null
            ? Output(string.Empty, exitCode: 1)
            : Output(branchName + "\n"));
    }

    private static void EnqueueFailureAt(
        StubGitCommandRunner runner,
        int failureIndex,
        OperationError error)
    {
        var successfulOutputs = SuccessfulOutputs();
        for (var index = 0; index < failureIndex; index++)
        {
            runner.Enqueue(successfulOutputs[index]);
        }

        runner.Enqueue(Result.Fail<GitCommandOutput>(error));
    }

    private static void EnqueueMalformedAt(StubGitCommandRunner runner, int failureIndex)
    {
        var successfulOutputs = SuccessfulOutputs();
        for (var index = 0; index < failureIndex; index++)
        {
            runner.Enqueue(successfulOutputs[index]);
        }

        runner.Enqueue(failureIndex switch
        {
            0 => Output("unexpected version\n"),
            1 or 2 => Output("maybe\n"),
            3 => Output("relative/root\n"),
            4 => Output(Sha1Revision.ToUpperInvariant() + "\n"),
            5 => Output(string.Empty, exitCode: 2),
            _ => throw new ArgumentOutOfRangeException(nameof(failureIndex)),
        });
    }

    private static Result<GitCommandOutput>[] SuccessfulOutputs() =>
    [
        Output("git version 2.51.0\n"),
        Output("true\n"),
        Output("false\n"),
        Output("/reported/root\n"),
        Output(Sha1Revision + "\n"),
        Output("main\n"),
    ];

    private static async Task<Result<RepositoryDescriptor>> StartTimeoutInspection(int activeCallIndex)
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        if (activeCallIndex == -2)
        {
            resolver.Enqueue(Result.Success<string>("/physical/selection"));
            resolver.Enqueue(WaitForCancellationAsync);
            var successfulOutputs = SuccessfulOutputs();
            for (var index = 0; index < 4; index++)
            {
                runner.Enqueue(successfulOutputs[index]);
            }
        }
        else if (activeCallIndex == -1)
        {
            resolver.Enqueue(WaitForCancellationAsync);
        }
        else
        {
            resolver.Enqueue(Result.Success<string>("/physical/selection"));
            resolver.Enqueue(Result.Success<string>("/physical/root"));
            var successfulOutputs = SuccessfulOutputs();
            for (var index = 0; index < activeCallIndex; index++)
            {
                runner.Enqueue(successfulOutputs[index]);
            }

            runner.Enqueue(WaitForCancellationAsync);
        }

        var inspector = new GitRepositoryInspector(runner, resolver);
        return await inspector.InspectAsync("/requested", CancellationToken.None);
    }

    private static async Task<Result<string>> WaitForCancellationAsync(
        string _,
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return Result.Success<string>("/unreachable");
    }

    private static async Task<Result<GitCommandOutput>> WaitForCancellationAsync(
        GitCommand _,
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return Output("unreachable");
    }

    private static void AssertInvalidPath(Result<RepositoryDescriptor> result)
    {
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(RepositoryErrorCode.InvalidPath, error.Code);
    }
}
