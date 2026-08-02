using System.Diagnostics;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Parsers;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Constants;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Core.Snapshots.Services;

/// <summary>
///     Captures the committed comparison between two exact revisions as a hashed, bounded snapshot manifest.
/// </summary>
/// <remarks>
///     <para>
///         The Engine registers this service as scoped. It serves one run and does not need to be thread-safe.
///     </para>
///     <para>
///         Capture reads local Git facts only, applies one action deadline, and refuses to record a manifest whose
///         accepted revisions moved since the run was accepted.
///     </para>
/// </remarks>
/// <param name="commandRunner">The controlled Git process boundary. Cannot be <see langword="null" />.</param>
/// <param name="fileSummaryComposer">The comparison file-summary composer. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for capture flow and outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="commandRunner" />, <paramref name="fileSummaryComposer" />, or <paramref name="logger" /> is
///     <see langword="null" />.
/// </exception>
public sealed class GitSnapshotCaptureService(
    IGitCommandRunner commandRunner,
    IComparisonFileSummaryComposer fileSummaryComposer,
    ILogger<GitSnapshotCaptureService> logger)
    : ISnapshotCaptureService
{
    private static readonly OperationError StaleError = OperationError.Conflict(
        "The comparison changed between acceptance and the capture cut.", AnalysisFailureCode.StaleAtCapture);

    private static readonly OperationError TimedOutError = OperationError.Timeout(
        "Snapshot capture exceeded its allowed time.", AnalysisFailureCode.CaptureFailed);

    private static readonly OperationError TooLargeError = OperationError.UnprocessableInput(
        "The captured change exceeds the supported snapshot limit.", AnalysisFailureCode.CaptureFailed);

    private static readonly OperationError CaptureFailedError = OperationError.ExternalDependencyFailure(
        "Snapshot capture could not read the committed comparison.", AnalysisFailureCode.CaptureFailed);

    private readonly IGitCommandRunner _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));

    private readonly IComparisonFileSummaryComposer _fileSummaryComposer =
        fileSummaryComposer ?? throw new ArgumentNullException(nameof(fileSummaryComposer));

    private readonly ILogger<GitSnapshotCaptureService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<Result<SnapshotCapture>> CaptureAsync(AnalysisRunDetail run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(SnapshotLimits.CaptureTimeoutInSeconds);
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        try
        {
            var canonicalPath = run.Repository.CanonicalPath;

            var targetRevisionResult = await this.RunAsync(
                canonicalPath, startedAt, ["rev-parse", "--verify", run.Comparison.Target + "^{commit}"], actionCancellation.Token);
            if (targetRevisionResult.IsFailure)
            {
                return Result.ErrorFromResult<SnapshotCapture>(targetRevisionResult);
            }

            var parsedTargetRevision = ParseSingleRevision(targetRevisionResult.Data!);
            if (parsedTargetRevision.IsFailure)
            {
                return Result.ErrorFromResult<SnapshotCapture>(parsedTargetRevision);
            }

            var headRevisionResult = await this.RunAsync(canonicalPath, startedAt,
                ["rev-parse", "--verify", "HEAD^{commit}"], actionCancellation.Token);
            if (headRevisionResult.IsFailure)
            {
                return Result.ErrorFromResult<SnapshotCapture>(headRevisionResult);
            }

            var parsedHeadRevision = ParseSingleRevision(headRevisionResult.Data!);
            if (parsedHeadRevision.IsFailure)
            {
                return Result.ErrorFromResult<SnapshotCapture>(parsedHeadRevision);
            }

            var targetRevision = parsedTargetRevision.Data!;
            var headRevision = parsedHeadRevision.Data!;

            if (!StringComparer.Ordinal.Equals(targetRevision, run.Comparison.TargetRevision) ||
                !StringComparer.Ordinal.Equals(headRevision, run.Repository.HeadRevision))
            {
                this._logger.LogWarning("Snapshot capture for run {RunId} was rejected as stale because the target or " +
                    "HEAD revision changed since acceptance.", run.RunId);
                return StaleError;
            }

            var mergeBaseResult = await this.RunAsync(canonicalPath, startedAt,
                ["merge-base", "--all", targetRevision, headRevision], actionCancellation.Token);
            if (mergeBaseResult.IsFailure)
            {
                return Result.ErrorFromResult<SnapshotCapture>(mergeBaseResult);
            }

            if (IsQuietNoMergeBase(mergeBaseResult.Data!))
            {
                this._logger.LogWarning("Snapshot capture for run {RunId} found no merge base between the accepted " +
                    "revisions.", run.RunId);
                return CaptureFailedError;
            }

            var parsedMergeBases = GitComparisonOutputParser.ParseMergeBases(mergeBaseResult.Data!);
            if (parsedMergeBases.IsFailure || parsedMergeBases.Data!.Count != 1)
            {
                this._logger.LogWarning("Snapshot capture for run {RunId} found an unexpected number of merge bases " +
                    "between the accepted revisions.", run.RunId);
                return CaptureFailedError;
            }

            var mergeBaseRevision = parsedMergeBases.Data[0];

            var committedFilesResult = await this.RunAsync(
                canonicalPath, startedAt, GitComparisonCommandArguments.RawDiff(mergeBaseRevision, headRevision), actionCancellation.Token);
            if (committedFilesResult.IsFailure)
            {
                return Result.ErrorFromResult<SnapshotCapture>(committedFilesResult);
            }

            var parsedCommittedFiles = GitComparisonOutputParser.ParseCommittedFiles(committedFilesResult.Data!);
            if (parsedCommittedFiles.IsFailure)
            {
                return CaptureFailedError;
            }

            if (parsedCommittedFiles.Data!.Count > SnapshotLimits.MaximumManifestEntries)
            {
                this._logger.LogWarning("Snapshot capture for run {RunId} exceeded the supported manifest entry limit.", run.RunId);
                return TooLargeError;
            }

            var statusResult = await this.RunAsync(canonicalPath, startedAt, GitComparisonCommandArguments.Status(), actionCancellation.Token);
            if (statusResult.IsFailure)
            {
                return Result.ErrorFromResult<SnapshotCapture>(statusResult);
            }

            var parsedWorkingTree = GitComparisonOutputParser.ParseWorkingTree(statusResult.Data!);
            if (parsedWorkingTree.IsFailure)
            {
                return CaptureFailedError;
            }

            var summaryResult = this._fileSummaryComposer.Compose(
                ComparisonFileRecordComposer.Compose(parsedCommittedFiles.Data!, parsedWorkingTree.Data!));
            if (summaryResult.IsFailure)
            {
                return CaptureFailedError;
            }

            var summary = summaryResult.Data!;
            var excludedCounts = new ExcludedUncommittedCounts(summary.UncommittedFileTotal, summary.StagedFileCount,
                summary.UnstagedFileCount, summary.UntrackedFileCount, summary.ConflictedFileCount);

            var entries = parsedCommittedFiles.Data!.Select(ToEntry).ToArray();
            var manifestHash = SnapshotManifestFingerprint.Create(
                run.Repository.CanonicalRepositoryPathKey, run.Comparison.Target, targetRevision, headRevision, mergeBaseRevision, entries);

            var manifest = new SnapshotManifest(
                Guid.NewGuid(),
                manifestHash,
                run.Repository.CanonicalRepositoryPathKey,
                run.Comparison.Target,
                targetRevision,
                headRevision,
                mergeBaseRevision,
                entries);

            this._logger.LogInformation("Captured snapshot for run {RunId} with {EntryCount} entries and " +
                "{ExcludedUncommittedTotal} excluded uncommitted lineages in {ElapsedMilliseconds:0.000} ms.", run.RunId,
                entries.Length, excludedCounts.Total, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return Result.Success(new SnapshotCapture(manifest, excludedCounts));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            this._logger.LogWarning("Snapshot capture for run {RunId} timed out after {ElapsedMilliseconds:0.000} ms.",
                run.RunId, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return TimedOutError;
        }
    }

    /// <summary>
    ///     Runs one fixed capture command with the action's remaining time and stream bounds.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository root. Cannot be <see langword="null" />.</param>
    /// <param name="startedAt">The monotonic timestamp at which capture began.</param>
    /// <param name="subcommandArguments">The fixed Git subcommand arguments. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>A task that represents the asynchronous operation. The task result contains bounded Git output.</returns>
    private Task<Result<GitCommandOutput>> RunAsync(
        string canonicalPath, long startedAt, IReadOnlyList<string> subcommandArguments, CancellationToken cancellationToken)
    {
        var remaining = Remaining(startedAt);
        if (remaining <= TimeSpan.Zero)
        {
            return Task.FromResult<Result<GitCommandOutput>>(TimedOutError);
        }

        return this._commandRunner.RunAsync(
            new GitCommand(
                GitComparisonCommandArguments.Direct(canonicalPath, subcommandArguments),
                remaining,
                SnapshotLimits.MaximumCaptureOutputBytes,
                SnapshotLimits.MaximumDiagnosticBytes,
                CaptureErrors()),
            cancellationToken);
    }

    /// <summary>
    ///     Requires exactly one full revision from a successful quiet Git command.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>The parsed revision or the stable capture-failed error.</returns>
    private static Result<string> ParseSingleRevision(GitCommandOutput output)
    {
        var parsed = GitComparisonOutputParser.ParseMergeBases(output);
        return parsed.IsSuccess && parsed.Data!.Count == 1
            ? Result.Success<string>(parsed.Data[0])
            : CaptureFailedError;
    }

    /// <summary>
    ///     Maps one parsed committed raw-diff record to its manifest entry.
    /// </summary>
    /// <param name="record">The parsed committed file record. Cannot be <see langword="null" />.</param>
    /// <returns>The manifest entry carrying the record's exact Git facts.</returns>
    private static SnapshotManifestEntry ToEntry(GitComparisonFileRecord record) =>
        new(record.Path, record.OriginalPath, ToCategory(record.Status), record.SourceMode, record.TargetMode, record.SourceObjectId, record.TargetObjectId);

    /// <summary>
    ///     Maps one Git raw-diff status to its snapshot change category.
    /// </summary>
    /// <param name="status">The Git raw-diff status.</param>
    /// <returns>The mapped category.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status" /> is not a supported raw-diff status.</exception>
    private static SnapshotChangeCategory ToCategory(GitRawDiffStatus status) => status switch
    {
        GitRawDiffStatus.Added => SnapshotChangeCategory.Added,
        GitRawDiffStatus.Deleted => SnapshotChangeCategory.Deleted,
        GitRawDiffStatus.Modified => SnapshotChangeCategory.Modified,
        GitRawDiffStatus.TypeChanged => SnapshotChangeCategory.TypeChanged,
        GitRawDiffStatus.Renamed => SnapshotChangeCategory.Renamed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The raw diff status is not supported."),
    };

    /// <summary>
    ///     Recognizes only Git's quiet no-merge-base command outcome.
    /// </summary>
    /// <param name="output">The captured merge-base output. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> for exit code one and two empty streams.</returns>
    private static bool IsQuietNoMergeBase(GitCommandOutput output) =>
        output is { ExitCode: 1, StandardOutput.Length: 0, StandardError.Length: 0 };

    /// <summary>
    ///     Calculates the time remaining in the single capture budget.
    /// </summary>
    /// <param name="startedAt">The monotonic timestamp at which capture began.</param>
    /// <returns>The remaining duration, which can be nonpositive.</returns>
    private static TimeSpan Remaining(long startedAt) => SnapshotLimits.CaptureTimeoutInSeconds - Stopwatch.GetElapsedTime(startedAt);

    /// <summary>
    ///     Creates the immutable terminal-error policy for one capture command.
    /// </summary>
    /// <returns>The capture timeout, output-limit, and inspection errors.</returns>
    private static GitCommandErrorPolicy CaptureErrors() => new(TimedOutError, TooLargeError, CaptureFailedError);
}
