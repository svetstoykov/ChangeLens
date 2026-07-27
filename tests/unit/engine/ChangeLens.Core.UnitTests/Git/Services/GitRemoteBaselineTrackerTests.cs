using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.UnitTests.Git.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Core.UnitTests.Git.Services;

/// <summary>
///     Verifies remote baseline detection and refresh semantics.
/// </summary>
public sealed class GitRemoteBaselineTrackerTests
{
    private const string CanonicalPath = "/canonical/repository";
    private const string LocalRevision = "0123456789abcdef0123456789abcdef01234567";
    private const string MovedRevision = "89abcdef0123456789abcdef0123456789abcdef";
    private const string Target = "refs/remotes/origin/dev";

    /// <summary>
    ///     Rejects a target outside the remote-tracking namespace before any Git or path-resolution I/O.
    /// </summary>
    /// <param name="target">The unapproved supplied target.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("refs/heads/dev")]
    [InlineData("refs/remotes/origin")]
    [InlineData("refs/remotes/origin/")]
    public async Task CheckAsyncRejectsUnapprovedTargetBeforeIo(string? target)
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var tracker = CreateTracker(runner, resolver);

        var result = await tracker.CheckAsync(
            "/selected",
            target,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.UnprocessableInput, ComparisonErrorCode.TargetInvalid);
        Assert.Empty(runner.Commands);
        Assert.Empty(resolver.Paths);
    }

    /// <summary>
    ///     Returns <see cref="RemoteBaselineState.NoRemote" /> without a network call for a repository with no
    ///     configured remotes.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsNoRemoteWithoutNetworkCallForZeroConfiguredRemotes()
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var tracker = CreateTracker(runner, resolver);
        EnqueueInspection(runner, resolver);
        runner.Enqueue(Output(string.Empty));

        var result = await tracker.CheckAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RemoteBaselineState.NoRemote, result.Data!.State);
        Assert.Null(result.Data.RemoteRevision);
        Assert.DoesNotContain(runner.Commands, command => command.Arguments.Contains("ls-remote"));
    }

    /// <summary>
    ///     Returns current when the server advertises the same revision as the local cached reference.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsCurrentWhenRemoteRevisionMatchesLocalCache()
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var tracker = CreateTracker(runner, resolver);
        EnqueueInspection(runner, resolver);
        runner.Enqueue(Output("origin\n"));
        runner.Enqueue(Output(LocalRevision + "\n"));
        runner.Enqueue(Output(LocalRevision + "\trefs/heads/dev\n"));

        var result = await tracker.CheckAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RemoteBaselineState.Current, result.Data!.State);
        Assert.Equal(LocalRevision, result.Data.RemoteRevision);
    }

    /// <summary>
    ///     Returns moved when the server advertises a revision that differs from the local cached reference.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsMovedWhenRemoteRevisionDiffersFromLocalCache()
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var tracker = CreateTracker(runner, resolver);
        EnqueueInspection(runner, resolver);
        runner.Enqueue(Output("origin\n"));
        runner.Enqueue(Output(LocalRevision + "\n"));
        runner.Enqueue(Output(MovedRevision + "\trefs/heads/dev\n"));

        var result = await tracker.CheckAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RemoteBaselineState.Moved, result.Data!.State);
        Assert.Equal(MovedRevision, result.Data.RemoteRevision);
    }

    /// <summary>
    ///     Resolves a slashed branch name against the longest matching configured remote.
    /// </summary>
    [Fact]
    public async Task CheckAsyncResolvesSlashedBranchAgainstLongestMatchingRemote()
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var tracker = CreateTracker(runner, resolver);
        EnqueueInspection(runner, resolver);
        runner.Enqueue(Output("origin\norigin-mirror\n"));
        runner.Enqueue(Output(LocalRevision + "\n"));
        runner.Enqueue(Output(LocalRevision + "\trefs/heads/feature/foo\n"));

        var result = await tracker.CheckAsync(
            "/selected",
            "refs/remotes/origin/feature/foo",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RemoteBaselineState.Current, result.Data!.State);
        var lsRemoteCommand = Assert.Single(
            runner.Commands,
            command => command.Arguments.Contains("ls-remote"));
        Assert.Contains("origin", lsRemoteCommand.Arguments);
        Assert.Contains("feature/foo", lsRemoteCommand.Arguments);
    }

    /// <summary>
    ///     Fails with <see cref="ComparisonErrorCode.NoRemoteConfigured" /> when refresh finds no configured remote.
    /// </summary>
    [Fact]
    public async Task RefreshAsyncFailsWithNoRemoteConfiguredForZeroConfiguredRemotes()
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var tracker = CreateTracker(runner, resolver);
        EnqueueInspection(runner, resolver);
        runner.Enqueue(Output(string.Empty));

        var result = await tracker.RefreshAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.UnprocessableInput, ComparisonErrorCode.NoRemoteConfigured);
        Assert.DoesNotContain(runner.Commands, command => command.Arguments.Contains("fetch"));
    }

    /// <summary>
    ///     Returns the new revision after a successful scoped fetch.
    /// </summary>
    [Fact]
    public async Task RefreshAsyncReturnsNewRevisionAfterSuccessfulFetch()
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var tracker = CreateTracker(runner, resolver);
        EnqueueInspection(runner, resolver);
        runner.Enqueue(Output("origin\n"));
        runner.Enqueue(Output(string.Empty));
        runner.Enqueue(Output(MovedRevision + "\n"));

        var result = await tracker.RefreshAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(MovedRevision, result.Data);
        var fetchCommand = Assert.Single(runner.Commands, command => command.Arguments.Contains("fetch"));
        Assert.Contains("origin", fetchCommand.Arguments);
        Assert.Contains("refs/heads/dev:refs/remotes/origin/dev", fetchCommand.Arguments);
    }

    /// <summary>
    ///     Classifies a nonzero-exit fetch using the shared remote-failure diagnostic classifier.
    /// </summary>
    [Fact]
    public async Task RefreshAsyncClassifiesFailedFetchAsRemoteAuthenticationRequired()
    {
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        var tracker = CreateTracker(runner, resolver);
        EnqueueInspection(runner, resolver);
        runner.Enqueue(Output("origin\n"));
        runner.Enqueue(Output(string.Empty, exitCode: 128, standardError: "fatal: Authentication failed"));

        var result = await tracker.RefreshAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.Unauthorized, ComparisonErrorCode.RemoteAuthenticationRequired);
    }

    private static GitRemoteBaselineTracker CreateTracker(
        StubGitCommandRunner runner,
        StubRepositoryPathResolver resolver) =>
        new(new GitRepositoryInspector(runner, resolver, NullLogger<GitRepositoryInspector>.Instance),
            runner,
            NullLogger<GitRemoteBaselineTracker>.Instance);

    private static void EnqueueInspection(
        StubGitCommandRunner runner,
        StubRepositoryPathResolver resolver,
        string branchName = "main")
    {
        resolver.Enqueue(Result.Success<string>("/physical/selection"));
        resolver.Enqueue(Result.Success<string>(CanonicalPath));
        runner.Enqueue(Output("git version 2.51.0\n"));
        runner.Enqueue(Output("true\n"));
        runner.Enqueue(Output("false\n"));
        runner.Enqueue(Output(CanonicalPath + "\n"));
        runner.Enqueue(Output(LocalRevision + "\n"));
        runner.Enqueue(Output(branchName + "\n"));
    }

    private static Result<GitCommandOutput> Output(
        string standardOutput,
        int exitCode = 0,
        string standardError = "") =>
        Result.Success(new GitCommandOutput(exitCode, standardOutput, standardError));

    private static void AssertFailure<T>(
        Result<T> result,
        ErrorType expectedType,
        string expectedCode)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedType, error.Type);
        Assert.Equal(expectedCode, error.Code);
    }
}
