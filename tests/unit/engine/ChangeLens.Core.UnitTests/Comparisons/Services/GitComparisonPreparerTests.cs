using System.Reflection;
using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.UnitTests.Comparisons.Support;
using Xunit;

namespace ChangeLens.Core.UnitTests.Comparisons.Services;

/// <summary>
///     Verifies exact, bounded, and stable merge-base comparison preparation.
/// </summary>
public sealed class GitComparisonPreparerTests
{
    private const string Target = "refs/heads/topic";
    private const string ZeroSha1Revision = "0000000000000000000000000000000000000000";

    /// <summary>
    ///     Verifies unsupported target shapes are rejected before repository or Git inspection.
    /// </summary>
    /// <param name="target">The unsupported target value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("-topic")]
    [InlineData("topic")]
    [InlineData("refs/tags/v1")]
    [InlineData("refs/heads/topic^{commit}")]
    [InlineData("refs/notes/topic")]
    [InlineData("refs/remotes/origin")]
    public async Task PrepareAsyncRejectsUnsupportedTargetBeforeInspection(string? target)
    {
        var fixture = new ComparisonGitFixture();

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            target,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.UnprocessableInput, ComparisonErrorCode.TargetInvalid);
        Assert.Empty(fixture.Runner.Commands);
        Assert.Empty(fixture.Resolver.Paths);
    }

    /// <summary>
    ///     Verifies a target above the Unicode scalar bound is rejected before inspection.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncRejectsOverlongTargetBeforeInspection()
    {
        var fixture = new ComparisonGitFixture();
        var target = "refs/heads/" +
                     string.Concat(
                         Enumerable.Repeat(
                             "😀",
                             ComparisonLimits.MaximumRefScalars));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            target,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.UnprocessableInput, ComparisonErrorCode.TargetInvalid);
        Assert.Empty(fixture.Runner.Commands);
        Assert.Empty(fixture.Resolver.Paths);
    }

    /// <summary>
    ///     Verifies a target excluded from the selectable discovery set is rejected as unsupported.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncRejectsNoncommitOrSymbolicTarget()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target(Target, objectType: "blob"));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.UnprocessableInput, ComparisonErrorCode.TargetInvalid);
        Assert.Equal(7, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies a discovered symbolic cached remote HEAD is not selectable.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncRejectsSymbolicRemoteHeadTarget()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target(
                "refs/remotes/origin/HEAD",
                symbolicTarget: "refs/remotes/origin/main"),
            ComparisonGitFixture.Target("refs/remotes/origin/main"));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            "refs/remotes/origin/HEAD",
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.UnprocessableInput, ComparisonErrorCode.TargetInvalid);
        Assert.Equal(7, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies a selected ref deleted after discovery is reported as unavailable.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncReturnsUnavailableWhenSelectedRefDisappearsAfterDiscovery()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target(Target, ComparisonGitFixture.OtherSha1Revision));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty, exitCode: 1));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.NotFound, ComparisonErrorCode.TargetUnavailable);
        Assert.Equal(9, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies quiet merge-base exit code one maps narrowly to unrelated history.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncReturnsUnrelatedHistoryForQuietMergeBaseExitOne()
    {
        var fixture = new ComparisonGitFixture();
        EnqueueThroughBeginningStatus(fixture);
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty, exitCode: 1));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.UnprocessableInput, ComparisonErrorCode.UnrelatedHistory);
        Assert.Equal(11, fixture.Runner.Commands.Count);
        Assert.DoesNotContain(
            fixture.Runner.Commands,
            command => command.Arguments.Contains("rev-list", StringComparer.Ordinal));
        Assert.DoesNotContain(
            fixture.Runner.Commands,
            command => command.Arguments.Contains("diff", StringComparer.Ordinal));
    }

    /// <summary>
    ///     Verifies multiple best merge bases are rejected before count, diff, or ending-status inspection.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncReturnsAmbiguousBaseForMultipleMergeBases()
    {
        var fixture = new ComparisonGitFixture();
        EnqueueThroughBeginningStatus(fixture);
        fixture.Runner.Enqueue(
            ComparisonGitFixture.Output(
                ComparisonGitFixture.BaseSha1Revision + "\n" +
                ComparisonGitFixture.OtherSha1Revision + "\n"));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(result, ErrorType.UnprocessableInput, ComparisonErrorCode.AmbiguousBase);
        Assert.Equal(11, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies malformed or noisy merge-base output remains an inspection failure.
    /// </summary>
    /// <param name="output">The malformed command output.</param>
    [Theory]
    [MemberData(nameof(MalformedMergeBaseOutputs))]
    public async Task PrepareAsyncRejectsMalformedMergeBaseOutput(GitCommandOutput output)
    {
        var fixture = new ComparisonGitFixture();
        EnqueueThroughBeginningStatus(fixture);
        fixture.Runner.Enqueue(Result.Success(output));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(
            result,
            ErrorType.ExternalDependencyFailure,
            ComparisonErrorCode.InspectionFailed);
        Assert.Equal(11, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies left/right divergence mapping and the complete fixed command sequence.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncMapsCountsAndRunsExactSafeSequence()
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueuePreparation(counts: "3\t5\n");

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.TargetOnlyCommitCount);
        Assert.Equal(5, result.Data.CurrentWorkCommitCount);
        Assert.Equal(ComparisonReadiness.Ready, result.Data.Readiness);
        Assert.Equal(18, fixture.Runner.Commands.Count);
        Assert.Equal(
            DirectArguments("check-ref-format", Target),
            fixture.Runner.Commands[7].Arguments);
        Assert.Equal(
            DirectArguments("rev-parse", "--verify", Target + "^{commit}"),
            fixture.Runner.Commands[8].Arguments);
        Assert.Equal(
            DirectArguments(
                "status",
                "--porcelain=v2",
                "-z",
                "--untracked-files=all",
                "--ignore-submodules=none",
                "--find-renames=50%"),
            fixture.Runner.Commands[9].Arguments);
        Assert.Equal(
            DirectArguments(
                "merge-base",
                "--all",
                ComparisonGitFixture.OtherSha1Revision,
                ComparisonGitFixture.Sha1Revision),
            fixture.Runner.Commands[10].Arguments);
        Assert.Equal(
            DirectArguments(
                "rev-list",
                "--left-right",
                "--count",
                ComparisonGitFixture.OtherSha1Revision +
                "..." +
                ComparisonGitFixture.Sha1Revision,
                "--"),
            fixture.Runner.Commands[11].Arguments);
        Assert.Equal(
            DirectArguments(
                "diff",
                "--raw",
                "-z",
                "--no-abbrev",
                "--full-index",
                "--find-renames=50%",
                "--no-ext-diff",
                "--no-textconv",
                ComparisonGitFixture.BaseSha1Revision,
                ComparisonGitFixture.Sha1Revision,
                "--"),
            fixture.Runner.Commands[12].Arguments);
        Assert.Equal(
            DirectArguments("rev-parse", "--verify", "HEAD^{commit}"),
            fixture.Runner.Commands[14].Arguments);
        Assert.Equal(
            DirectArguments("symbolic-ref", "--quiet", "--short", "HEAD"),
            fixture.Runner.Commands[15].Arguments);
        Assert.Equal(
            DirectArguments("rev-parse", "--verify", Target + "^{commit}"),
            fixture.Runner.Commands[16].Arguments);
        Assert.Equal(
            fixture.Runner.Commands[9].Arguments,
            fixture.Runner.Commands[13].Arguments);
        Assert.All(
            fixture.Runner.Commands.Skip(7).Take(10),
            command =>
            {
                Assert.Equal(
                    ComparisonLimits.MaximumFactOutputBytes,
                    command.MaximumStandardOutputBytes);
                Assert.Equal(
                    ComparisonLimits.MaximumDiagnosticBytes,
                    command.MaximumStandardErrorBytes);
                Assert.True(command.Timeout > TimeSpan.Zero);
                Assert.True(command.Timeout <= ComparisonLimits.PreparationTimeout);
            });
    }

    /// <summary>
    ///     Verifies empty, working-tree-only, and conflicted readiness with overlapping aggregate counts.
    /// </summary>
    /// <param name="counts">The commit-count output.</param>
    /// <param name="committed">The committed raw-diff output.</param>
    /// <param name="working">The porcelain-v2 working-tree output.</param>
    /// <param name="readiness">The expected readiness.</param>
    /// <param name="changed">The expected changed-file total.</param>
    /// <param name="uncommitted">The expected uncommitted-file total.</param>
    /// <param name="staged">The expected staged-file count.</param>
    /// <param name="unstaged">The expected unstaged-file count.</param>
    /// <param name="untracked">The expected untracked-file count.</param>
    /// <param name="conflicted">The expected conflicted-file count.</param>
    [Theory]
    [MemberData(nameof(ReadinessCases))]
    public async Task PrepareAsyncComposesFilesAndReadiness(
        string counts,
        string committed,
        string working,
        ComparisonReadiness readiness,
        int changed,
        int uncommitted,
        int staged,
        int unstaged,
        int untracked,
        int conflicted)
    {
        var fixture = new ComparisonGitFixture();
        fixture.EnqueuePreparation(
            counts: counts,
            committedFiles: committed,
            workingTree: working);

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(readiness, result.Data!.Readiness);
        Assert.Equal(changed, result.Data.Files.ChangedFileTotal);
        Assert.Equal(uncommitted, result.Data.Files.UncommittedFileTotal);
        Assert.Equal(staged, result.Data.Files.StagedFileCount);
        Assert.Equal(unstaged, result.Data.Files.UnstagedFileCount);
        Assert.Equal(untracked, result.Data.Files.UntrackedFileCount);
        Assert.Equal(conflicted, result.Data.Files.ConflictedFileCount);
    }

    /// <summary>
    ///     Verifies every reviewed committed and working-tree record kind reaches aggregate composition.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncComposesEveryReviewedFileRecordKind()
    {
        var committed =
            Raw(
                "000000",
                "100644",
                ZeroSha1Revision,
                ComparisonGitFixture.Sha1Revision,
                "A",
                "added.cs") +
            Raw(
                "100644",
                "100644",
                ComparisonGitFixture.Sha1Revision,
                ComparisonGitFixture.OtherSha1Revision,
                "M",
                "modified.cs") +
            Raw(
                "100644",
                "000000",
                ComparisonGitFixture.Sha1Revision,
                ZeroSha1Revision,
                "D",
                "deleted.cs") +
            Raw(
                "100644",
                "100644",
                ComparisonGitFixture.Sha1Revision,
                ComparisonGitFixture.OtherSha1Revision,
                "R100",
                "renamed.cs",
                "old-name.cs") +
            Raw(
                "100644",
                "120000",
                ComparisonGitFixture.Sha1Revision,
                ComparisonGitFixture.OtherSha1Revision,
                "T",
                "typed.cs") +
            Raw(
                "160000",
                "160000",
                ComparisonGitFixture.Sha1Revision,
                ComparisonGitFixture.OtherSha1Revision,
                "M",
                "module");
        var working =
            Ordinary("M.", "staged.cs") +
            Ordinary(".M", "unstaged.cs") +
            Ordinary("MM", "both.cs") +
            Rename("working-renamed.cs", "working-old.cs") +
            $"1 T. N... 100644 120000 120000 {ComparisonGitFixture.Sha1Revision} " +
            $"{ComparisonGitFixture.OtherSha1Revision} working-type\0" +
            $"1 .M S.M. 160000 160000 160000 {ComparisonGitFixture.Sha1Revision} " +
            $"{ComparisonGitFixture.Sha1Revision} working-module\0" +
            "? untracked.cs\0" +
            "! ignored.cs\0" +
            Unmerged("UU", "conflicted.cs");
        var fixture = new ComparisonGitFixture();
        fixture.EnqueuePreparation(
            counts: "0\t1\n",
            committedFiles: committed,
            workingTree: working);

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(14, result.Data!.Files.ChangedFileTotal);
        Assert.Equal(8, result.Data.Files.UncommittedFileTotal);
        Assert.Equal(5, result.Data.Files.StagedFileCount);
        Assert.Equal(4, result.Data.Files.UnstagedFileCount);
        Assert.Equal(1, result.Data.Files.UntrackedFileCount);
        Assert.Equal(1, result.Data.Files.ConflictedFileCount);
        Assert.Equal(ComparisonReadiness.Conflicts, result.Data.Readiness);
    }

    /// <summary>
    ///     Verifies every re-read consistency fact participates in change detection.
    /// </summary>
    /// <param name="changedFact">The ending fact to change.</param>
    [Theory]
    [InlineData("head")]
    [InlineData("branch")]
    [InlineData("target")]
    [InlineData("targets")]
    [InlineData("status")]
    public async Task PrepareAsyncRejectsFactsChangedDuringPreparation(string changedFact)
    {
        var fixture = new ComparisonGitFixture();
        EnqueueThroughCommittedFacts(fixture, string.Empty);
        fixture.Runner.Enqueue(
            ComparisonGitFixture.Output(
                changedFact == "status" ? "? changed.cs\0" : string.Empty));
        fixture.Runner.Enqueue(
            ComparisonGitFixture.Output(
                (changedFact == "head"
                    ? ComparisonGitFixture.BaseSha1Revision
                    : ComparisonGitFixture.Sha1Revision) + "\n"));
        fixture.Runner.Enqueue(
            ComparisonGitFixture.Output(
                changedFact == "branch" ? "other\n" : "main\n"));
        fixture.Runner.Enqueue(
            ComparisonGitFixture.Output(
                (changedFact == "target"
                    ? ComparisonGitFixture.BaseSha1Revision
                    : ComparisonGitFixture.OtherSha1Revision) + "\n"));
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target(
                changedFact == "targets" ? "refs/heads/another" : Target,
                ComparisonGitFixture.OtherSha1Revision));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(
            result,
            ErrorType.Conflict,
            ComparisonErrorCode.ChangedDuringPreparation);
        Assert.Equal(18, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies only the reviewed missing-target diagnostic becomes consistency drift.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncTreatsConfirmedEndingTargetDeletionAsChanged()
    {
        var fixture = new ComparisonGitFixture();
        EnqueueThroughEndingHead(fixture);
        fixture.Runner.Enqueue(
            ComparisonGitFixture.Output(
                string.Empty,
                exitCode: 128,
                standardError: "fatal: Needed a single revision\n"));
        fixture.EnqueueTargets();

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(
            result,
            ErrorType.Conflict,
            ComparisonErrorCode.ChangedDuringPreparation);
        Assert.Equal(18, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies unexpected or malformed ending target resolution remains an inspection failure.
    /// </summary>
    /// <param name="output">The unexpected ending target-resolution output.</param>
    [Theory]
    [MemberData(nameof(UnexpectedEndingTargetOutputs))]
    public async Task PrepareAsyncRejectsUnexpectedEndingTargetResolution(
        GitCommandOutput output)
    {
        var fixture = new ComparisonGitFixture();
        EnqueueThroughEndingHead(fixture);
        fixture.Runner.Enqueue(Result.Success(output));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        AssertFailure(
            result,
            ErrorType.ExternalDependencyFailure,
            ComparisonErrorCode.InspectionFailed);
        Assert.Equal(17, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies successful preparations over identical facts produce the same freshness token.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncProducesDeterministicFreshnessToken()
    {
        var firstFixture = new ComparisonGitFixture();
        firstFixture.EnqueuePreparation(workingTree: "? local.cs\0");
        var secondFixture = new ComparisonGitFixture();
        secondFixture.EnqueuePreparation(workingTree: "? local.cs\0");

        var first = await firstFixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);
        var second = await secondFixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Data!.FreshnessToken, second.Data!.FreshnessToken);
        Assert.Equal(ComparisonLimits.FingerprintHexLength, first.Data.FreshnessToken.Length);
    }

    /// <summary>
    ///     Verifies caller cancellation remains exception-based and runs no external operation.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncPropagatesCallerCancellation()
    {
        var fixture = new ComparisonGitFixture();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Preparer.PrepareAsync("/selected", Target, cancellation.Token));
        Assert.Empty(fixture.Runner.Commands);
        Assert.Empty(fixture.Resolver.Paths);
    }

    /// <summary>
    ///     Verifies source operation errors are forwarded by object identity.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncForwardsSourceErrorsLosslessly()
    {
        var sourceError = OperationError.Unauthorized("denied", "repository.denied");
        var fixture = new ComparisonGitFixture();
        fixture.Resolver.Enqueue(Result.Fail<string>(sourceError));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.Same(sourceError, Assert.Single(result.Errors));
    }

    /// <summary>
    ///     Verifies every source error is forwarded in order and by reference.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncForwardsMultipleSourceErrorsLosslessly()
    {
        var first = OperationError.Timeout("first", "source.first");
        var second = OperationError.Unauthorized("second", "source.second");
        var fixture = new ComparisonGitFixture();
        EnqueueThroughBeginningStatus(fixture);
        fixture.Runner.Enqueue(MultipleFailure<GitCommandOutput>(first, second));

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Errors.Count);
        Assert.Same(first, result.Errors[0]);
        Assert.Same(second, result.Errors[1]);
        Assert.Equal(11, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies a timeout from every direct preparation command boundary is forwarded without later commands.
    /// </summary>
    /// <param name="boundary">The zero-based direct command boundary.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public async Task PrepareAsyncForwardsTimeoutAtEveryDirectCommandBoundary(
        int boundary)
    {
        var timeout = OperationError.Timeout(
            "timed out",
            ComparisonErrorCode.TimedOut);
        var fixture = new ComparisonGitFixture();
        EnqueueFailureAtDirectBoundary(fixture, boundary, timeout);

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.Same(timeout, Assert.Single(result.Errors));
        Assert.Equal(8 + boundary, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies timeout errors from repository inspection and initial discovery are forwarded without later commands.
    /// </summary>
    /// <param name="boundary">The zero-based repository or initial-discovery command boundary.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task PrepareAsyncForwardsTimeoutAtEveryInitialCommandBoundary(
        int boundary)
    {
        var timeout = OperationError.Timeout(
            "timed out",
            ComparisonErrorCode.TimedOut);
        var fixture = new ComparisonGitFixture();
        EnqueueFailureAtInitialBoundary(fixture, boundary, timeout);

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.Same(timeout, Assert.Single(result.Errors));
        Assert.Equal(boundary + 1, fixture.Runner.Commands.Count);
    }

    /// <summary>
    ///     Verifies output-limit failures are forwarded without exposing partial comparison facts.
    /// </summary>
    [Fact]
    public async Task PrepareAsyncForwardsOutputLimitFailure()
    {
        var tooLarge = OperationError.UnprocessableInput(
            "too large",
            ComparisonErrorCode.TooLarge);
        var fixture = new ComparisonGitFixture();
        EnqueueFailureAtDirectBoundary(fixture, 5, tooLarge);

        var result = await fixture.Preparer.PrepareAsync(
            "/selected",
            Target,
            TestContext.Current.CancellationToken);

        Assert.Same(tooLarge, Assert.Single(result.Errors));
        Assert.Null(result.Data);
    }

    /// <summary>
    ///     Gets malformed merge-base command outcomes.
    /// </summary>
    /// <returns>The malformed outputs.</returns>
    public static IEnumerable<object[]> MalformedMergeBaseOutputs()
    {
        yield return [new GitCommandOutput(0, "not-an-object\n", string.Empty)];
        yield return [new GitCommandOutput(1, string.Empty, "unexpected")];
        yield return [new GitCommandOutput(2, string.Empty, string.Empty)];
    }

    /// <summary>
    ///     Gets ending target-resolution outcomes that do not confirm a missing ref.
    /// </summary>
    /// <returns>The unexpected or malformed outputs.</returns>
    public static IEnumerable<object[]> UnexpectedEndingTargetOutputs()
    {
        yield return [new GitCommandOutput(1, string.Empty, string.Empty)];
        yield return [new GitCommandOutput(2, string.Empty, string.Empty)];
        yield return [new GitCommandOutput(128, string.Empty, "fatal: secret failure\n")];
        yield return [new GitCommandOutput(0, "not-an-object\n", string.Empty)];
    }

    /// <summary>
    ///     Gets file-composition and readiness cases.
    /// </summary>
    /// <returns>The controlled parser outputs and expected aggregate facts.</returns>
    public static IEnumerable<object[]> ReadinessCases()
    {
        yield return
        [
            "0\t0\n",
            string.Empty,
            string.Empty,
            ComparisonReadiness.Empty,
            0,
            0,
            0,
            0,
            0,
            0,
        ];
        yield return
        [
            "0\t0\n",
            string.Empty,
            "? local.cs\0! ignored.cs\0",
            ComparisonReadiness.Ready,
            1,
            1,
            0,
            0,
            1,
            0,
        ];
        yield return
        [
            "0\t2\n",
            Raw("100644", "100644", "M", "committed.cs"),
            Ordinary("MM", "overlap.cs") +
            Unmerged("UU", "conflicted.cs") +
            "? local.cs\0",
            ComparisonReadiness.Conflicts,
            4,
            3,
            2,
            2,
            1,
            1,
        ];
    }

    private static void EnqueueThroughBeginningStatus(ComparisonGitFixture fixture)
    {
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target(Target, ComparisonGitFixture.OtherSha1Revision));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));
        fixture.Runner.Enqueue(
            ComparisonGitFixture.Output(ComparisonGitFixture.OtherSha1Revision + "\n"));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));
    }

    private static void EnqueueThroughCommittedFacts(
        ComparisonGitFixture fixture,
        string workingTree)
    {
        EnqueueThroughBeginningStatus(fixture);
        fixture.Runner.Enqueue(
            ComparisonGitFixture.Output(ComparisonGitFixture.BaseSha1Revision + "\n"));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output("0\t0\n"));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));
    }

    private static void EnqueueThroughEndingHead(ComparisonGitFixture fixture)
    {
        EnqueueThroughCommittedFacts(fixture, string.Empty);
        fixture.Runner.Enqueue(ComparisonGitFixture.Output(string.Empty));
        fixture.Runner.Enqueue(
            ComparisonGitFixture.Output(ComparisonGitFixture.Sha1Revision + "\n"));
        fixture.Runner.Enqueue(ComparisonGitFixture.Output("main\n"));
    }

    private static void EnqueueFailureAtInitialBoundary(
        ComparisonGitFixture fixture,
        int boundary,
        OperationError error)
    {
        fixture.Resolver.Enqueue(Result.Success<string>("/physical/selection"));
        if (boundary >= 4)
        {
            fixture.Resolver.Enqueue(
                Result.Success<string>(ComparisonGitFixture.CanonicalPath));
        }

        var successes = new[]
        {
            ComparisonGitFixture.Output("git version 2.51.0\n"),
            ComparisonGitFixture.Output("true\n"),
            ComparisonGitFixture.Output("false\n"),
            ComparisonGitFixture.Output(ComparisonGitFixture.CanonicalPath + "\n"),
            ComparisonGitFixture.Output(ComparisonGitFixture.Sha1Revision + "\n"),
            ComparisonGitFixture.Output("main\n"),
        };
        for (var index = 0; index < boundary && index < successes.Length; index++)
        {
            fixture.Runner.Enqueue(successes[index]);
        }

        fixture.Runner.Enqueue(Result.Fail<GitCommandOutput>(error));
    }

    private static void EnqueueFailureAtDirectBoundary(
        ComparisonGitFixture fixture,
        int boundary,
        OperationError error)
    {
        fixture.EnqueueInspection();
        fixture.EnqueueTargets(
            ComparisonGitFixture.Target(Target, ComparisonGitFixture.OtherSha1Revision));
        var successes = new[]
        {
            ComparisonGitFixture.Output(string.Empty),
            ComparisonGitFixture.Output(ComparisonGitFixture.OtherSha1Revision + "\n"),
            ComparisonGitFixture.Output(string.Empty),
            ComparisonGitFixture.Output(ComparisonGitFixture.BaseSha1Revision + "\n"),
            ComparisonGitFixture.Output("0\t0\n"),
            ComparisonGitFixture.Output(string.Empty),
            ComparisonGitFixture.Output(string.Empty),
            ComparisonGitFixture.Output(ComparisonGitFixture.Sha1Revision + "\n"),
            ComparisonGitFixture.Output("main\n"),
            ComparisonGitFixture.Output(ComparisonGitFixture.OtherSha1Revision + "\n"),
        };

        for (var index = 0; index < boundary && index < successes.Length; index++)
        {
            fixture.Runner.Enqueue(successes[index]);
        }

        if (boundary < successes.Length)
        {
            fixture.Runner.Enqueue(Result.Fail<GitCommandOutput>(error));
            return;
        }

        fixture.Runner.Enqueue(Result.Fail<GitCommandOutput>(error));
    }

    private static IReadOnlyList<string> DirectArguments(
        params string[] subcommandArguments) =>
        [
            "-C",
            ComparisonGitFixture.CanonicalPath,
            "-c",
            "core.fsmonitor=false",
            "-c",
            "diff.external=",
            "-c",
            "diff.trustExitCode=false",
            "-c",
            "diff.renames=true",
            .. subcommandArguments,
        ];

    private static string Raw(
        string oldMode,
        string newMode,
        string status,
        string path) =>
        Raw(
            oldMode,
            newMode,
            ComparisonGitFixture.Sha1Revision,
            ComparisonGitFixture.OtherSha1Revision,
            status,
            path);

    private static string Raw(
        string oldMode,
        string newMode,
        string oldRevision,
        string newRevision,
        string status,
        string path,
        string? originalPath = null) =>
        originalPath is null
            ? $":{oldMode} {newMode} {oldRevision} {newRevision} {status}\0{path}\0"
            : $":{oldMode} {newMode} {oldRevision} {newRevision} {status}\0" +
              $"{originalPath}\0{path}\0";

    private static string Ordinary(string status, string path)
    {
        var headRevision = status[0] == 'A'
            ? ZeroSha1Revision
            : ComparisonGitFixture.Sha1Revision;
        var indexRevision = status[0] switch
        {
            'D' => ZeroSha1Revision,
            '.' => headRevision,
            _ => ComparisonGitFixture.OtherSha1Revision,
        };
        return $"1 {status} N... 100644 100644 100644 " +
               $"{headRevision} {indexRevision} {path}\0";
    }

    private static string Rename(
        string path,
        string originalPath) =>
        $"2 R. N... 100644 100644 100644 {ComparisonGitFixture.Sha1Revision} " +
        $"{ComparisonGitFixture.OtherSha1Revision} R100 {path}\0{originalPath}\0";

    private static string Unmerged(string status, string path) =>
        $"u {status} N... 100644 100644 100644 100644 " +
        $"{ComparisonGitFixture.Sha1Revision} {ComparisonGitFixture.OtherSha1Revision} " +
        $"{ComparisonGitFixture.Sha1Revision} {path}\0";

    private static Result<T> MultipleFailure<T>(
        OperationError first,
        params OperationError[] remaining)
    {
        var result = Result.Fail<T>(first);
        var errorsField = typeof(Result).GetField(
            "_errors",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var errors = Assert.IsType<List<OperationError>>(errorsField!.GetValue(result));
        errors.AddRange(remaining);
        return result;
    }

    private static void AssertFailure(
        Result<PreparedComparison> result,
        ErrorType expectedType,
        string expectedCode)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedType, error.Type);
        Assert.Equal(expectedCode, error.Code);
    }
}
