using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.UnitTests.Comparisons.Support;
using Xunit;

namespace ChangeLens.Core.UnitTests.Comparisons.Services;

/// <summary>
///     Verifies bounded comparison freshness inspection semantics.
/// </summary>
public sealed class GitComparisonFreshnessCheckerTests
{
    private const string Target = "refs/heads/topic";

    /// <summary>
    ///     Rejects malformed freshness tokens before repository access.
    /// </summary>
    /// <param name="freshnessToken">The malformed supplied token.</param>
    [Theory]
    [MemberData(nameof(InvalidFreshnessTokens))]
    public async Task CheckAsyncRejectsMalformedFreshnessTokenBeforeIo(string? freshnessToken)
    {
        var fixture = new ComparisonGitFixture();

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            freshnessToken,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.Validation, ComparisonErrorCode.InvalidFreshnessToken);
        Assert.Empty(fixture.Runner.Commands);
        Assert.Empty(fixture.Resolver.Paths);
    }

    /// <summary>
    ///     Rejects an unapproved target before repository access.
    /// </summary>
    [Fact]
    public async Task CheckAsyncRejectsUnapprovedTargetBeforeIo()
    {
        var fixture = new ComparisonGitFixture();

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            "topic",
            CurrentToken(),
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.UnprocessableInput, ComparisonErrorCode.TargetInvalid);
        Assert.Empty(fixture.Runner.Commands);
        Assert.Empty(fixture.Resolver.Paths);
    }

    /// <summary>
    ///     Returns current when every fingerprint fact is unchanged.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsCurrentForUnchangedFacts()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueFreshnessCheck(workingTree: "? local.cs\0");

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(workingTree: "? local.cs\0"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Current, result.Data);
        Assert.Equal(10, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Returns stale when the current branch identity changes at the same revision.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsStaleWhenBranchIdentityChanges()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueFreshnessCheck(branchName: "other");

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data);
    }

    /// <summary>
    ///     Returns stale when HEAD moves to another revision.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsStaleWhenHeadRevisionChanges()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection(revision: ComparisonGitFixture.BaseSha1Revision);
        fixture.EnqueueTargets(ComparisonGitFixture.Target(Target, ComparisonGitFixture.OtherSha1Revision));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(ComparisonGitFixture.OtherSha1Revision + "\n"));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data);
    }

    /// <summary>
    ///     Returns stale when HEAD becomes detached at the same revision.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsStaleWhenHeadBecomesDetached()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueDetachedInspection();
        fixture.EnqueueTargets(ComparisonGitFixture.Target(Target, ComparisonGitFixture.OtherSha1Revision));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(ComparisonGitFixture.OtherSha1Revision + "\n"));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data);
    }

    /// <summary>
    ///     Returns stale when the selected ref disappears after preparation.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsStaleWhenSelectedTargetIsMissing()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(ComparisonGitFixture.Target("refs/heads/other"));

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data);
        Assert.Equal(7, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Returns stale when a previously supported target becomes unsupported.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsStaleWhenSelectedTargetBecomesUnsupported()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(ComparisonGitFixture.Target(Target, objectType: "blob"));

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data);
    }

    /// <summary>
    ///     Returns stale when the selected target revision changes.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsStaleWhenTargetRevisionChanges()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueFreshnessCheck(ComparisonGitFixture.BaseSha1Revision);

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data);
    }

    /// <summary>
    ///     Returns stale when the complete discovered target set changes.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsStaleWhenTargetSetChanges()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target(Target, ComparisonGitFixture.OtherSha1Revision),
            ComparisonGitFixture.Target("refs/heads/other", ComparisonGitFixture.BaseSha1Revision));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(ComparisonGitFixture.OtherSha1Revision + "\n"));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data);
    }

    /// <summary>
    ///     Returns stale when normalized working-tree status changes.
    /// </summary>
    [Fact]
    public async Task CheckAsyncReturnsStaleWhenWorkingTreeStatusChanges()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueFreshnessCheck(workingTree: "? added.cs\0");

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Stale, result.Data);
    }

    /// <summary>
    ///     Returns current when file content changes without changing normalized status facts.
    /// </summary>
    [Fact]
    public async Task CheckAsyncKeepsSameStatusFactsCurrent()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueFreshnessCheck(workingTree: "1 .M N... 100644 100644 100644 " +
            ComparisonGitFixture.Sha1Revision + " " + ComparisonGitFixture.Sha1Revision + " local.cs\0");

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(workingTree: "1 .M N... 100644 100644 100644 " +
                ComparisonGitFixture.Sha1Revision + " " + ComparisonGitFixture.Sha1Revision + " local.cs\0"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ComparisonFreshnessState.Current, result.Data);
    }

    /// <summary>
    ///     Forwards repository failures without classifying them as stale.
    /// </summary>
    [Fact]
    public async Task CheckAsyncForwardsRepositoryFailureLosslessly()
    {
        var sourceError = OperationError.ExternalDependencyFailure("unavailable", "repository.unavailable");
        var fixture = new ComparisonGitFixture();
        fixture.Resolver.Enqueue(Result.Fail<string>(sourceError));

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.Same(sourceError, Assert.Single(result.Errors));
    }

    /// <summary>
    ///     Maps malformed status output to the stable inspection failure.
    /// </summary>
    [Fact]
    public async Task CheckAsyncRejectsMalformedStatusOutput()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueFreshnessCheck(workingTree: "malformed\0");

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.ExternalDependencyFailure, ComparisonErrorCode.InspectionFailed);
    }

    /// <summary>
    ///     Forwards output-limit failures instead of classifying them as stale.
    /// </summary>
    [Fact]
    public async Task CheckAsyncForwardsOutputLimitFailure()
    {
        var tooLarge = OperationError.UnprocessableInput("too large", ComparisonErrorCode.TooLarge);
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(ComparisonGitFixture.Target(Target, ComparisonGitFixture.OtherSha1Revision));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(ComparisonGitFixture.OtherSha1Revision + "\n"));
        fixture.Runner.Enqueue(Result.Fail<GitCommandOutput>(tooLarge));

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.Same(tooLarge, Assert.Single(result.Errors));
    }

    /// <summary>
    ///     Gives every command only the remaining ten-second freshness budget.
    /// </summary>
    [Fact]
    public async Task CheckAsyncUsesSingleFreshnessBudgetForEveryCommand()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueFreshnessCheck();

        var result = await fixture.FreshnessChecker.CheckAsync(
            "/selected",
            Target,
            CurrentToken(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.All(
            fixture.Runner.Commands,
            command => Assert.InRange(
                command.Timeout,
                TimeSpan.Zero,
                ComparisonLimits.FreshnessTimeout));
    }

    /// <summary>
    ///     Keeps caller cancellation exception-based without repository access.
    /// </summary>
    [Fact]
    public async Task CheckAsyncPropagatesCallerCancellation()
    {
        var fixture = new ComparisonGitFixture();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.FreshnessChecker.CheckAsync("/selected", Target, CurrentToken(), cancellation.Token));
        Assert.Empty(fixture.Runner.Commands);
        Assert.Empty(fixture.Resolver.Paths);
    }

    /// <summary>
    ///     Gets structurally invalid freshness-token values.
    /// </summary>
    /// <returns>The invalid token values.</returns>
    public static IEnumerable<object?[]> InvalidFreshnessTokens()
    {
        yield return [null];
        yield return [new string('a', 63)];
        yield return [new string('A', 64)];
        yield return [new string('g', 64)];
    }

    private static string CurrentToken(string workingTree = "")
    {
        var repository = new RepositoryDescriptor(
            "repository",
            ComparisonGitFixture.CanonicalPath,
            new BranchRepositoryHead("main", ComparisonGitFixture.Sha1Revision));
        var target = new ComparisonTargetDescriptor(
            ComparisonTargetKind.Local,
            "topic",
            Target,
            ComparisonGitFixture.OtherSha1Revision);
        var targetSetToken = ComparisonFingerprint.CreateTargetSetToken(
            repository.CanonicalPath,
            null,
            [target],
            0);
        var parsedStatus = ChangeLens.Core.Git.Parsers.GitComparisonOutputParser.ParseWorkingTree(
            ComparisonGitFixture.Output(workingTree).Data!);
        Assert.True(parsedStatus.IsSuccess);
        return ComparisonFingerprint.CreateFreshnessToken(
            repository,
            target,
            targetSetToken,
            parsedStatus.Data!);
    }

    private static void AssertFailure(
        Result<ComparisonFreshnessState> result,
        ErrorType type,
        string code)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(type, error.Type);
        Assert.Equal(code, error.Code);
    }
}
