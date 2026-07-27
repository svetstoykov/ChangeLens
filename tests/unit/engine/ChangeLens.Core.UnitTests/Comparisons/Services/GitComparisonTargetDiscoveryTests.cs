using System.Diagnostics;
using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.UnitTests.Comparisons.Support;
using ChangeLens.Core.UnitTests.Git.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Core.UnitTests.Comparisons.Services;

/// <summary>
///     Verifies bounded local and cached remote comparison-target discovery.
/// </summary>
public sealed class GitComparisonTargetDiscoveryTests
{
    /// <summary>
    ///     Verifies that omitted and empty queries are distinct valid paging identities.
    /// </summary>
    [Fact]
    public async Task ListAsyncAcceptsOmittedAndEmptyQueries()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(ComparisonGitFixture.Target("refs/heads/topic"));
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(ComparisonGitFixture.Target("refs/heads/topic"));

        var omitted = await fixture.Discovery.ListAsync(
            "/selected", null, null, null, TestContext.Current.CancellationToken);
        var empty = await fixture.Discovery.ListAsync(
            "/selected", string.Empty, null, null, TestContext.Current.CancellationToken);

        Assert.True(omitted.IsSuccess);
        Assert.True(empty.IsSuccess);
        Assert.Single(omitted.Data!.Targets);
        Assert.Single(empty.Data!.Targets);
        Assert.NotEqual(omitted.Data.TargetSetToken, empty.Data.TargetSetToken);
    }

    /// <summary>
    ///     Verifies the exact 256-Unicode-scalar query boundary with supplementary characters.
    /// </summary>
    [Fact]
    public async Task ListAsyncAcceptsQueryAtScalarBoundary()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets();
        var query = string.Concat(Enumerable.Repeat("😀", ComparisonLimits.MaximumQueryScalars));

        var result = await fixture.Discovery.ListAsync(
            "/selected", query, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    ///     Verifies invalid query shapes are rejected without running Git.
    /// </summary>
    /// <param name="query">The invalid query.</param>
    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task ListAsyncRejectsInvalidQueryBeforeGit(string query)
    {
        var fixture = new ComparisonGitFixture();

        var result = await fixture.Discovery.ListAsync(
            "/selected", query, null, null, TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.Validation, ComparisonErrorCode.InvalidTargetQuery);
        Assert.Empty(fixture.Runner.Commands);
        Assert.Empty(fixture.Resolver.Paths);
    }

    /// <summary>
    ///     Verifies structurally invalid cursor-token requests are rejected before Git.
    /// </summary>
    /// <param name="after">The cursor value.</param>
    /// <param name="token">The target-set token.</param>
    [Theory]
    [InlineData("refs/heads/main", null)]
    [InlineData(null, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("refs/heads/main", "short")]
    [InlineData("refs/heads/main", "0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public async Task ListAsyncRejectsInvalidPageShapeBeforeGit(string? after, string? token)
    {
        var fixture = new ComparisonGitFixture();

        var result = await fixture.Discovery.ListAsync(
            "/selected", null, after, token, TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.Validation, ComparisonErrorCode.InvalidTargetPage);
        Assert.Empty(fixture.Runner.Commands);
        Assert.Empty(fixture.Resolver.Paths);
    }

    /// <summary>
    ///     Verifies a cursor above the scalar bound is rejected before Git.
    /// </summary>
    [Fact]
    public async Task ListAsyncRejectsOverlongCursorBeforeGit()
    {
        var fixture = new ComparisonGitFixture();
        var after = "refs/heads/" +
                    string.Concat(Enumerable.Repeat("😀", ComparisonLimits.MaximumRefScalars));

        var result = await fixture.Discovery.ListAsync(
            "/selected",
            null,
            after,
            new string('0', ComparisonLimits.FingerprintHexLength),
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.Validation, ComparisonErrorCode.InvalidTargetPage);
        Assert.Empty(fixture.Runner.Commands);
    }

    /// <summary>
    ///     Verifies a valid token is compared before the cursor is searched.
    /// </summary>
    [Fact]
    public async Task ListAsyncReturnsTargetsChangedBeforeCursorValidity()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(ComparisonGitFixture.Target("refs/heads/topic"));

        var result = await fixture.Discovery.ListAsync(
            "/selected",
            null,
            "refs/heads/missing",
            new string('0', ComparisonLimits.FingerprintHexLength),
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.Conflict, ComparisonErrorCode.TargetsChanged);
    }

    /// <summary>
    ///     Verifies an absent cursor is rejected only after the current set and token are validated.
    /// </summary>
    [Fact]
    public async Task ListAsyncRejectsCursorAbsentFromFilteredSet()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(ComparisonGitFixture.Target("refs/heads/topic"));
        var expectedTargets =
            new[]
            {
                new ComparisonTargetDescriptor(
                    ComparisonTargetKind.Local,
                    "topic",
                    "refs/heads/topic",
                    ComparisonGitFixture.Sha1Revision),
            };
        var token = ComparisonFingerprint.CreateTargetSetToken(
            ComparisonGitFixture.CanonicalPath, null, expectedTargets, 0);

        var result = await fixture.Discovery.ListAsync(
            "/selected",
            null,
            "refs/heads/missing",
            token,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.Validation, ComparisonErrorCode.InvalidTargetPage);
        Assert.Equal(7, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies continuation results retain the complete filtered set for deterministic Engine page shaping.
    /// </summary>
    [Fact]
    public async Task ListAsyncRetainsUnpagedTargetsAfterCursor()
    {
        var records = new[]
        {
            ComparisonGitFixture.Target("refs/heads/a"),
            ComparisonGitFixture.Target("refs/heads/b"),
            ComparisonGitFixture.Target("refs/heads/c"),
        };
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(records);
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(records);
        var first = await fixture.Discovery.ListAsync(
            "/selected",
            null,
            null,
            null,
            TestContext.Current.CancellationToken);

        var continuation = await fixture.Discovery.ListAsync(
            "/selected",
            null,
            "refs/heads/a",
            first.Data!.TargetSetToken,
            TestContext.Current.CancellationToken);

        Assert.True(continuation.IsSuccess);
        Assert.Equal(
            ["refs/heads/b", "refs/heads/c"],
            continuation.Data!.Targets.Select(target => target.FullName));
        Assert.Equal(
            ["refs/heads/a", "refs/heads/b", "refs/heads/c"],
            continuation.Data.UnpagedTargets.Select(target => target.FullName));
    }

    /// <summary>
    ///     Verifies target classification, ordering, display names, unsupported counts, and configured default selection.
    /// </summary>
    [Fact]
    public async Task ListAsyncDiscoversOrderedSupportedTargetsAndConfiguredDefault()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        var overlong = "refs/heads/" + new string('x', ComparisonLimits.MaximumRefScalars);
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target("refs/remotes/origin/z"),
            ComparisonGitFixture.Target("refs/heads/z"),
            ComparisonGitFixture.Target("refs/heads/main", upstreamRemote: "upstream"),
            ComparisonGitFixture.Target("refs/heads/a", ComparisonGitFixture.OtherSha1Revision),
            ComparisonGitFixture.Target("refs/remotes/alpha/b"),
            ComparisonGitFixture.Target("refs/remotes/origin/HEAD", symbolicTarget: "refs/remotes/origin/z"),
            ComparisonGitFixture.Target("refs/remotes/upstream/default"),
            ComparisonGitFixture.Target(
                "refs/remotes/upstream/HEAD",
                symbolicTarget: "refs/remotes/upstream/default"),
            ComparisonGitFixture.Target("refs/tags/v1"),
            ComparisonGitFixture.Target("refs/heads/blob", objectType: "blob"),
            ComparisonGitFixture.Target("refs/other/name"),
            ComparisonGitFixture.Target(overlong));

        var result = await fixture.Discovery.ListAsync(
            "/selected", null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var set = result.Data!;
        Assert.Equal(
            [
                "refs/heads/a",
                "refs/heads/z",
                "refs/remotes/alpha/b",
                "refs/remotes/origin/z",
                "refs/remotes/upstream/default",
            ],
            set.Targets.Select(target => target.FullName));
        Assert.Equal(
            ["a", "z", "alpha/b", "origin/z", "upstream/default"],
            set.Targets.Select(target => target.Name));
        Assert.Equal(
            [
                ComparisonTargetKind.Local,
                ComparisonTargetKind.Local,
                ComparisonTargetKind.RemoteTracking,
                ComparisonTargetKind.RemoteTracking,
                ComparisonTargetKind.RemoteTracking,
            ],
            set.Targets.Select(target => target.Kind));
        Assert.Equal("refs/remotes/upstream/default", set.SuggestedTarget!.FullName);
        Assert.Equal(4, set.UnsupportedTargetCount);
        Assert.DoesNotContain(set.Targets, target => target.FullName == overlong);
        Assert.Equal(
            ComparisonGitFixture.OtherSha1Revision,
            set.Targets[0].Revision);
    }

    /// <summary>
    ///     Verifies the exact command form, separate arguments, limits, and action-wide remaining timeout.
    /// </summary>
    [Fact]
    public async Task ListAsyncRunsOnlyTheApprovedBoundedRefCommand()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets();

        var result = await fixture.Discovery.ListAsync(
            "/selected", null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var command = fixture.Runner.Commands[6];
        Assert.Equal(
            [
                "-C",
                ComparisonGitFixture.CanonicalPath,
                "-c",
                "diff.external=",
                "-c",
                "diff.trustExitCode=false",
                "for-each-ref",
                "--format=%(refname)%00%(objectname)%00%(objecttype)%00%(symref)%00%(upstream:remotename)%00",
                "refs/heads",
                "refs/remotes",
            ],
            command.Arguments);
        Assert.Equal(ComparisonLimits.MaximumFactOutputBytes, command.MaximumStandardOutputBytes);
        Assert.Equal(ComparisonLimits.MaximumDiagnosticBytes, command.MaximumStandardErrorBytes);
        Assert.True(command.Timeout > TimeSpan.Zero);
        Assert.True(command.Timeout <= ComparisonLimits.TargetDiscoveryTimeout);
        Assert.True(fixture.Runner.Commands[5].Timeout >= command.Timeout);
    }

    /// <summary>
    ///     Verifies detached HEAD excludes no local branch and equal revisions do not collapse identities.
    /// </summary>
    [Fact]
    public async Task ListAsyncKeepsEveryLocalIdentityForDetachedHead()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueDetachedInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target("refs/heads/main"),
            ComparisonGitFixture.Target("refs/heads/topic"));

        var result = await fixture.Discovery.ListAsync(
            "/selected", null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Targets.Count);
    }

    /// <summary>
    ///     Verifies ordinal case-insensitive search preserves exact returned target identities.
    /// </summary>
    [Fact]
    public async Task ListAsyncFiltersCaseInsensitivelyWithoutChangingIdentity()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target("refs/heads/Feature/Ж"),
            ComparisonGitFixture.Target("refs/remotes/origin/main"),
            ComparisonGitFixture.Target(
                "refs/remotes/origin/HEAD",
                symbolicTarget: "refs/remotes/origin/main"));

        var result = await fixture.Discovery.ListAsync(
            "/selected", "fEaTuRe", null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var target = Assert.Single(result.Data!.Targets);
        Assert.Equal("Feature/Ж", target.Name);
        Assert.Equal("refs/heads/Feature/Ж", target.FullName);
        Assert.Null(result.Data.SuggestedTarget);
    }

    /// <summary>
    ///     Verifies continuation returns only records after the exact cursor and omits a suggestion.
    /// </summary>
    [Fact]
    public async Task ListAsyncContinuesAfterExactCursorWithoutSuggestion()
    {
        var fixture = new ComparisonGitFixture();
        var records = new[]
        {
            ComparisonGitFixture.Target("refs/heads/a"),
            ComparisonGitFixture.Target("refs/heads/b"),
            ComparisonGitFixture.Target("refs/remotes/origin/main"),
            ComparisonGitFixture.Target(
                "refs/remotes/origin/HEAD",
                symbolicTarget: "refs/remotes/origin/main"),
        };
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(records);
        var first = await fixture.Discovery.ListAsync(
            "/selected", null, null, null, TestContext.Current.CancellationToken);
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(records);

        var continued = await fixture.Discovery.ListAsync(
            "/selected",
            null,
            "refs/heads/a",
            first.Data!.TargetSetToken,
            TestContext.Current.CancellationToken);

        Assert.True(continued.IsSuccess);
        Assert.Equal(
            ["refs/heads/b", "refs/remotes/origin/main"],
            continued.Data!.Targets.Select(target => target.FullName));
        Assert.Null(continued.Data.SuggestedTarget);
    }

    /// <summary>
    ///     Verifies configured and origin symbolic defaults are ignored when they resolve to no candidate.
    /// </summary>
    [Fact]
    public async Task ListAsyncDoesNotGuessOrExposeMissingSymbolicDefault()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target("refs/heads/main", upstreamRemote: "upstream"),
            ComparisonGitFixture.Target("refs/remotes/upstream/topic"),
            ComparisonGitFixture.Target(
                "refs/remotes/upstream/HEAD",
                symbolicTarget: "refs/remotes/upstream/missing"),
            ComparisonGitFixture.Target(
                "refs/remotes/origin/HEAD",
                symbolicTarget: "refs/remotes/origin/missing"));

        var result = await fixture.Discovery.ListAsync(
            "/selected", null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Targets);
        Assert.Null(result.Data.SuggestedTarget);
        Assert.Equal(0, result.Data.UnsupportedTargetCount);
    }

    /// <summary>
    ///     Verifies origin's symbolic default is used when no configured remote default is available.
    /// </summary>
    [Fact]
    public async Task ListAsyncFallsBackToOriginSymbolicDefault()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target("refs/heads/main"),
            ComparisonGitFixture.Target("refs/remotes/origin/trunk"),
            ComparisonGitFixture.Target(
                "refs/remotes/origin/HEAD",
                symbolicTarget: "refs/remotes/origin/trunk"));

        var result = await fixture.Discovery.ListAsync(
            "/selected", null, null, null, TestContext.Current.CancellationToken);

        Assert.Equal(
            "refs/remotes/origin/trunk",
            result.Data!.SuggestedTarget!.FullName);
    }

    /// <summary>
    ///     Verifies an empty validated target set succeeds.
    /// </summary>
    [Fact]
    public async Task ListAsyncSucceedsForEmptyTargetSet()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(ComparisonGitFixture.Target("refs/heads/main"));

        var result = await fixture.Discovery.ListAsync(
            "/selected", null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Targets);
        Assert.Null(result.Data.SuggestedTarget);
        Assert.Equal(0, result.Data.UnsupportedTargetCount);
        Assert.Equal(ComparisonLimits.FingerprintHexLength, result.Data.TargetSetToken.Length);
    }

    /// <summary>
    ///     Verifies repository failures are forwarded before target-token checks.
    /// </summary>
    [Fact]
    public async Task ListAsyncForwardsRepositoryFailureBeforeTokenComparison()
    {
        var error = OperationError.Unauthorized("denied", "repository.denied");
        var runner = new StubGitCommandRunner();
        var resolver = new StubRepositoryPathResolver();
        resolver.Enqueue(Result.Fail<string>(error));
        var discovery = new GitComparisonTargetDiscovery(
            new ChangeLens.Core.Git.Services.GitRepositoryInspector(
                runner, resolver, NullLogger<ChangeLens.Core.Git.Services.GitRepositoryInspector>.Instance),
            runner,
            NullLogger<GitComparisonTargetDiscovery>.Instance);

        var result = await discovery.ListAsync(
            "/selected",
            null,
            "refs/heads/missing",
            new string('0', ComparisonLimits.FingerprintHexLength),
            TestContext.Current.CancellationToken);

        Assert.Same(error, Assert.Single(result.Errors));
        Assert.Empty(runner.Commands);
    }

    /// <summary>
    ///     Verifies malformed ref output wins over a changed token.
    /// </summary>
    [Fact]
    public async Task ListAsyncReturnsParsingFailureBeforeTokenComparison()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.Runner.Enqueue(ComparisonGitFixture.Output("malformed"));

        var result = await fixture.Discovery.ListAsync(
            "/selected",
            null,
            "refs/heads/missing",
            new string('0', ComparisonLimits.FingerprintHexLength),
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.ExternalDependencyFailure, ComparisonErrorCode.InspectionFailed);
    }

    /// <summary>
    ///     Verifies an exhausted shared action budget returns the stable comparison timeout without running Git.
    /// </summary>
    [Fact]
    public async Task ListForRepositoryAsyncReturnsComparisonTimeoutWhenBudgetIsExhausted()
    {
        var fixture = new ComparisonGitFixture();
        var repository = new RepositoryDescriptor(
            "repository",
            ComparisonGitFixture.CanonicalPath,
            new BranchRepositoryHead("main", ComparisonGitFixture.Sha1Revision));

        var result = await fixture.Discovery.ListForRepositoryAsync(
            repository,
            null,
            null,
            null,
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 2),
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.Timeout, ComparisonErrorCode.TimedOut);
        Assert.Empty(fixture.Runner.Commands);
    }

    /// <summary>
    ///     Verifies caller cancellation remains exception-based.
    /// </summary>
    [Fact]
    public async Task ListForRepositoryAsyncPropagatesCallerCancellation()
    {
        var fixture = new ComparisonGitFixture();
        var repository = new RepositoryDescriptor(
            "repository",
            ComparisonGitFixture.CanonicalPath,
            new DetachedRepositoryHead(ComparisonGitFixture.Sha1Revision));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Discovery.ListForRepositoryAsync(
                repository,
                null,
                null,
                null,
                Stopwatch.GetTimestamp(),
                TimeSpan.FromSeconds(1),
                cancellation.Token));
        Assert.Empty(fixture.Runner.Commands);
    }

    /// <summary>
    ///     Gets malformed or overlong query values.
    /// </summary>
    /// <returns>The invalid query cases.</returns>
    public static IEnumerable<object[]> InvalidQueries()
    {
        yield return [new string('x', ComparisonLimits.MaximumQueryScalars + 1)];
        yield return [string.Concat(
            Enumerable.Repeat("😀", ComparisonLimits.MaximumQueryScalars + 1))];
        yield return ["contains\0nul"];
    }

    private static void AssertFailure(
        Result<ComparisonTargetSet> result,
        ErrorType expectedType,
        string expectedCode)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedType, error.Type);
        Assert.Equal(expectedCode, error.Code);
    }
}
