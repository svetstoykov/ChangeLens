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
    }

    /// <summary>
    ///     Gets the target-discovery service under test.
    /// </summary>
    internal GitComparisonTargetDiscovery Discovery { get; }

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
    internal void EnqueueInspection(string branchName = "main")
    {
        Resolver.Enqueue(Result.Success<string>("/physical/selection"));
        Resolver.Enqueue(Result.Success<string>(CanonicalPath));
        Runner.Enqueue(Output("git version 2.51.0\n"));
        Runner.Enqueue(Output("true\n"));
        Runner.Enqueue(Output("false\n"));
        Runner.Enqueue(Output(CanonicalPath + "\n"));
        Runner.Enqueue(Output(Sha1Revision + "\n"));
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
