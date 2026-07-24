using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.UnitTests.Git.Support;

namespace ChangeLens.Core.UnitTests.Comparisons.Support;

/// <summary>
///     Provides controlled repository inspection and comparison-ref output for comparison tests.
/// </summary>
internal sealed class ComparisonGitFixture
{
    /// <summary>
    ///     Defines a valid SHA-1 revision used by comparison tests.
    /// </summary>
    internal const string Sha1Revision = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>
    ///     Defines another valid SHA-1 revision used by comparison tests.
    /// </summary>
    internal const string OtherSha1Revision = "89abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    ///     Defines a merge-base SHA-1 revision used by comparison tests.
    /// </summary>
    internal const string BaseSha1Revision = "fedcba9876543210fedcba9876543210fedcba98";

    /// <summary>
    ///     Defines the canonical repository root returned by the fixture.
    /// </summary>
    internal const string CanonicalPath = "/canonical/repository";

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComparisonGitFixture" /> class.
    /// </summary>
    internal ComparisonGitFixture()
    {
        Runner = new StubGitCommandRunner();
        Resolver = new StubRepositoryPathResolver();
        Discovery = new GitComparisonTargetDiscovery(
            new GitRepositoryInspector(Runner, Resolver),
            Runner);
        Preparer = new GitComparisonPreparer(
            new GitRepositoryInspector(Runner, Resolver),
            Discovery,
            Runner,
            new ComparisonFileSummaryComposer());
        FreshnessChecker = new GitComparisonFreshnessChecker(
            new GitRepositoryInspector(Runner, Resolver),
            Discovery,
            Runner);
    }

    /// <summary>
    ///     Gets the target-discovery service under test.
    /// </summary>
    internal GitComparisonTargetDiscovery Discovery { get; }

    /// <summary>
    ///     Gets the comparison-preparation service under test.
    /// </summary>
    internal GitComparisonPreparer Preparer { get; }

    /// <summary>
    ///     Gets the comparison freshness checker under test.
    /// </summary>
    internal GitComparisonFreshnessChecker FreshnessChecker { get; }

    /// <summary>
    ///     Gets the controlled Git command runner.
    /// </summary>
    internal StubGitCommandRunner Runner { get; }

    /// <summary>
    ///     Gets the controlled physical-path resolver.
    /// </summary>
    internal StubRepositoryPathResolver Resolver { get; }

    /// <summary>
    ///     Queues a successful attached-branch repository inspection.
    /// </summary>
    /// <param name="branchName">The checked-out branch name.</param>
    internal void EnqueueInspection(
        string branchName = "main",
        string revision = Sha1Revision)
    {
        Resolver.Enqueue(Result.Success<string>("/physical/selection"));
        Resolver.Enqueue(Result.Success<string>(CanonicalPath));
        Runner.Enqueue(Output("git version 2.51.0\n"));
        Runner.Enqueue(Output("true\n"));
        Runner.Enqueue(Output("false\n"));
        Runner.Enqueue(Output(CanonicalPath + "\n"));
        Runner.Enqueue(Output(revision + "\n"));
        Runner.Enqueue(Output(branchName + "\n"));
    }

    /// <summary>
    ///     Queues a successful detached-HEAD repository inspection.
    /// </summary>
    internal void EnqueueDetachedInspection()
    {
        Resolver.Enqueue(Result.Success<string>("/physical/selection"));
        Resolver.Enqueue(Result.Success<string>(CanonicalPath));
        Runner.Enqueue(Output("git version 2.51.0\n"));
        Runner.Enqueue(Output("true\n"));
        Runner.Enqueue(Output("false\n"));
        Runner.Enqueue(Output(CanonicalPath + "\n"));
        Runner.Enqueue(Output(Sha1Revision + "\n"));
        Runner.Enqueue(Output(string.Empty, exitCode: 1));
    }

    /// <summary>
    ///     Queues strictly formatted comparison-ref records.
    /// </summary>
    /// <param name="records">The complete record lines without their final line feeds.</param>
    internal void EnqueueTargets(params string[] records) =>
        Runner.Enqueue(Output(string.Concat(records.Select(record => record + "\n"))));

    /// <summary>
    ///     Queues a complete stable comparison preparation with controlled fact output.
    /// </summary>
    /// <param name="targetRevision">The selected target revision.</param>
    /// <param name="mergeBaseRevision">The unique merge-base revision.</param>
    /// <param name="counts">The target-only and current-work count output.</param>
    /// <param name="committedFiles">The raw committed-file output.</param>
    /// <param name="workingTree">The beginning and ending working-tree output.</param>
    /// <param name="branchName">The beginning and ending branch name.</param>
    internal void EnqueuePreparation(
        string targetRevision = OtherSha1Revision,
        string mergeBaseRevision = BaseSha1Revision,
        string counts = "0\t0\n",
        string committedFiles = "",
        string workingTree = "",
        string branchName = "main")
    {
        EnqueueInspection(branchName);
        EnqueueTargets(Target("refs/heads/topic", targetRevision));
        Runner.Enqueue(Output(string.Empty));
        Runner.Enqueue(Output(targetRevision + "\n"));
        Runner.Enqueue(Output(workingTree));
        Runner.Enqueue(Output(mergeBaseRevision + "\n"));
        Runner.Enqueue(Output(counts));
        Runner.Enqueue(Output(committedFiles));
        Runner.Enqueue(Output(workingTree));
        Runner.Enqueue(Output(Sha1Revision + "\n"));
        Runner.Enqueue(Output(branchName + "\n"));
        Runner.Enqueue(Output(targetRevision + "\n"));
        EnqueueTargets(Target("refs/heads/topic", targetRevision));
    }

    /// <summary>
    ///     Queues preparation through the committed-diff command, which returns the supplied bounded-output failure.
    /// </summary>
    /// <param name="failure">The exact non-null failure returned for committed file facts.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="failure" /> is <see langword="null" />.
    /// </exception>
    internal void EnqueuePreparationWithCommittedFailure(OperationError failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        EnqueueInspection();
        EnqueueTargets(Target("refs/heads/topic", OtherSha1Revision));
        Runner.Enqueue(Output(string.Empty));
        Runner.Enqueue(Output(OtherSha1Revision + "\n"));
        Runner.Enqueue(Output(string.Empty));
        Runner.Enqueue(Output(BaseSha1Revision + "\n"));
        Runner.Enqueue(Output("0\t0\n"));
        Runner.Enqueue(Result.Fail<GitCommandOutput>(failure));
    }

    /// <summary>
    ///     Queues a complete comparison freshness check with controlled fact output.
    /// </summary>
    /// <param name="targetRevision">The selected target revision.</param>
    /// <param name="workingTree">The raw porcelain-v2 working-tree output.</param>
    /// <param name="branchName">The checked-out branch name.</param>
    internal void EnqueueFreshnessCheck(
        string targetRevision = OtherSha1Revision,
        string workingTree = "",
        string branchName = "main")
    {
        EnqueueInspection(branchName);
        EnqueueTargets(Target("refs/heads/topic", targetRevision));
        Runner.Enqueue(Output(string.Empty));
        Runner.Enqueue(Output(targetRevision + "\n"));
        Runner.Enqueue(Output(workingTree));
    }

    /// <summary>
    ///     Creates one strictly formatted comparison-ref record.
    /// </summary>
    /// <param name="fullName">The full Git ref name.</param>
    /// <param name="revision">The resolved object identifier.</param>
    /// <param name="objectType">The resolved Git object type.</param>
    /// <param name="symbolicTarget">The symbolic target, or <see langword="null" />.</param>
    /// <param name="upstreamRemote">The upstream remote, or <see langword="null" />.</param>
    /// <returns>The NUL-delimited record with its required terminal NUL.</returns>
    internal static string Target(
        string fullName,
        string revision = Sha1Revision,
        string objectType = "commit",
        string? symbolicTarget = null,
        string? upstreamRemote = null) =>
        string.Join(
            '\0',
            fullName,
            revision,
            objectType,
            symbolicTarget ?? string.Empty,
            upstreamRemote ?? string.Empty) + '\0';

    /// <summary>
    ///     Creates a successful Git output result.
    /// </summary>
    /// <param name="standardOutput">The captured standard output.</param>
    /// <param name="exitCode">The Git exit code.</param>
    /// <param name="standardError">The captured standard error.</param>
    /// <returns>A successful runner result containing the captured output.</returns>
    internal static Result<GitCommandOutput> Output(
        string standardOutput,
        int exitCode = 0,
        string standardError = "") =>
        Result.Success(new GitCommandOutput(exitCode, standardOutput, standardError));
}
