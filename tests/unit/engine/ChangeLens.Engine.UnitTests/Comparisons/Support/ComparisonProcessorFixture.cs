using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.UnitTests.Repositories.Support;

namespace ChangeLens.Engine.UnitTests.Comparisons.Support;

/// <summary>
///     Provides concrete comparison services backed by controlled Git and path collaborators.
/// </summary>
internal sealed class ComparisonProcessorFixture
{
    /// <summary>
    ///     Defines the current repository revision used by comparison processor tests.
    /// </summary>
    internal const string HeadRevision = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>
    ///     Defines the selected target revision used by comparison processor tests.
    /// </summary>
    internal const string TargetRevision = "89abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    ///     Defines the merge-base revision used by comparison processor tests.
    /// </summary>
    internal const string MergeBaseRevision = "fedcba9876543210fedcba9876543210fedcba98";

    /// <summary>
    ///     Defines the canonical repository path returned by the fixture.
    /// </summary>
    internal const string CanonicalPath = "/projects/change_lens";

    private readonly RepositoryInspectorFixture _repositoryFixture = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComparisonProcessorFixture" /> class.
    /// </summary>
    internal ComparisonProcessorFixture()
    {
        this.TargetDiscovery = new GitComparisonTargetDiscovery(this._repositoryFixture.Inspector, this._repositoryFixture);
        this.Preparer = new GitComparisonPreparer(this._repositoryFixture.Inspector, this.TargetDiscovery, this._repositoryFixture,
            new ComparisonFileSummaryComposer());
        this.FreshnessChecker = new GitComparisonFreshnessChecker(this._repositoryFixture.Inspector, this.TargetDiscovery, this._repositoryFixture);
        this.RemoteBaselineTracker = new GitRemoteBaselineTracker(this._repositoryFixture.Inspector, this._repositoryFixture);
    }

    /// <summary>
    ///     Gets the repository inspector used by repository-open processor tests.
    /// </summary>
    internal GitRepositoryInspector RepositoryInspector => this._repositoryFixture.Inspector;

    /// <summary>
    ///     Gets the comparison-target discovery service.
    /// </summary>
    internal GitComparisonTargetDiscovery TargetDiscovery { get; }

    /// <summary>
    ///     Gets the comparison preparer.
    /// </summary>
    internal GitComparisonPreparer Preparer { get; }

    /// <summary>
    ///     Gets the comparison freshness checker.
    /// </summary>
    internal GitComparisonFreshnessChecker FreshnessChecker { get; }

    /// <summary>
    ///     Gets the remote baseline tracker.
    /// </summary>
    internal GitRemoteBaselineTracker RemoteBaselineTracker { get; }

    /// <summary>
    ///     Gets the received Git commands in call order.
    /// </summary>
    internal List<GitCommand> Commands => this._repositoryFixture.Commands;

    /// <summary>
    ///     Gets the received selected paths in call order.
    /// </summary>
    internal List<string> Paths => this._repositoryFixture.Paths;

    /// <summary>
    ///     Queues a path-resolution callback.
    /// </summary>
    /// <param name="result">The callback invoked by the next path-resolution call.</param>
    internal void EnqueuePath(
        Func<string, CancellationToken, Task<Result<string>>> result) =>
        this._repositoryFixture.EnqueuePath(result);

    /// <summary>
    ///     Queues a complete successful repository inspection.
    /// </summary>
    /// <param name="branchName">The attached branch name.</param>
    /// <param name="revision">The current repository revision.</param>
    internal void EnqueueInspection(
        string branchName = "main",
        string revision = HeadRevision)
    {
        this._repositoryFixture.EnqueuePath(Result.Success<string>("/physical/selection"));
        this._repositoryFixture.EnqueuePath(Result.Success<string>(CanonicalPath));
        this._repositoryFixture.EnqueueCommand(Output("git version 2.51.0\n"));
        this._repositoryFixture.EnqueueCommand(Output("true\n"));
        this._repositoryFixture.EnqueueCommand(Output("false\n"));
        this._repositoryFixture.EnqueueCommand(Output(CanonicalPath + "\n"));
        this._repositoryFixture.EnqueueCommand(Output(revision + "\n"));
        this._repositoryFixture.EnqueueCommand(Output(branchName + "\n"));
    }

    /// <summary>
    ///     Queues one or more strict comparison-target records.
    /// </summary>
    /// <param name="records">The complete records without their final line feeds.</param>
    internal void EnqueueTargets(params string[] records) =>
        this._repositoryFixture.EnqueueCommand(
            Output(string.Concat(records.Select(record => record + "\n"))));

    /// <summary>
    ///     Queues a stable ready comparison preparation.
    /// </summary>
    internal void EnqueuePreparation()
    {
        this.EnqueueInspection();
        this.EnqueueTargets(Target("refs/heads/topic", TargetRevision));
        this._repositoryFixture.EnqueueCommand(Output(string.Empty));
        this._repositoryFixture.EnqueueCommand(Output(TargetRevision + "\n"));
        this._repositoryFixture.EnqueueCommand(Output("? local.cs\0"));
        this._repositoryFixture.EnqueueCommand(Output(MergeBaseRevision + "\n"));
        this._repositoryFixture.EnqueueCommand(Output("3\t5\n"));
        this._repositoryFixture.EnqueueCommand(Output(string.Empty));
        this._repositoryFixture.EnqueueCommand(Output("? local.cs\0"));
        this._repositoryFixture.EnqueueCommand(Output(HeadRevision + "\n"));
        this._repositoryFixture.EnqueueCommand(Output("main\n"));
        this._repositoryFixture.EnqueueCommand(Output(TargetRevision + "\n"));
        this.EnqueueTargets(Target("refs/heads/topic", TargetRevision));
    }

    /// <summary>
    ///     Queues a comparison freshness check that can be evaluated against a supplied token.
    /// </summary>
    internal void EnqueueFreshnessCheck()
    {
        this.EnqueueInspection();
        this.EnqueueTargets(Target("refs/heads/topic", TargetRevision));
        this._repositoryFixture.EnqueueCommand(Output(string.Empty));
        this._repositoryFixture.EnqueueCommand(Output(TargetRevision + "\n"));
        this._repositoryFixture.EnqueueCommand(Output("? local.cs\0"));
    }

    /// <summary>
    ///     Creates one strict NUL-delimited comparison-target record.
    /// </summary>
    /// <param name="fullName">The full Git reference name.</param>
    /// <param name="revision">The target revision.</param>
    /// <param name="upstreamRemote">The configured upstream remote, or <see langword="null" />.</param>
    /// <returns>The complete comparison-target record.</returns>
    internal static string Target(
        string fullName,
        string revision = HeadRevision,
        string? upstreamRemote = null) =>
        string.Join(
            '\0',
            fullName,
            revision,
            "commit",
            string.Empty,
            upstreamRemote ?? string.Empty) + '\0';

    private static Result<GitCommandOutput> Output(string standardOutput, int exitCode = 0) =>
        Result.Success(new GitCommandOutput(exitCode, standardOutput, string.Empty));
}
