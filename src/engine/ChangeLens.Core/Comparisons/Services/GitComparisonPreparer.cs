using System.Buffers;
using System.Diagnostics;
using System.Text;
using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Parsers;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Core.Comparisons.Services;

/// <summary>
///     Prepares immutable comparison facts for one exact local Git target.
/// </summary>
/// <remarks>
///     <para>
///         The Engine registers this service as scoped. It serves one request and does not need to be thread-safe.
///     </para>
///     <para>
///         Preparation reads local Git facts only, applies one action deadline, and rejects facts that change
///         before the stable result is assembled.
///     </para>
/// </remarks>
/// <param name="repositoryInspector">The repository inspection service. Cannot be <see langword="null" />.</param>
/// <param name="targetDiscovery">The comparison-target discovery service. Cannot be <see langword="null" />.</param>
/// <param name="commandRunner">The controlled Git process boundary. Cannot be <see langword="null" />.</param>
/// <param name="fileSummaryComposer">The comparison file-summary composer. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for preparation flow and outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="repositoryInspector" />, <paramref name="targetDiscovery" />,
///     <paramref name="commandRunner" />, <paramref name="fileSummaryComposer" />, or <paramref name="logger" /> is
///     <see langword="null" />.
/// </exception>
public sealed class GitComparisonPreparer(
    IGitRepositoryInspector repositoryInspector,
    IGitComparisonTargetDiscovery targetDiscovery,
    IGitCommandRunner commandRunner,
    IComparisonFileSummaryComposer fileSummaryComposer,
    ILogger<GitComparisonPreparer> logger)
    : IGitComparisonPreparer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly OperationError TimedOutError = OperationError.Timeout(
        "Comparison inspection exceeded its allowed time.",
        ComparisonErrorCode.TimedOut);

    private static readonly OperationError TooLargeError = OperationError.UnprocessableInput(
        "The comparison exceeds the supported local inspection limit.",
        ComparisonErrorCode.TooLarge);

    private static readonly OperationError InspectionFailedError =
        OperationError.ExternalDependencyFailure(
            "Git comparison inspection failed.",
            ComparisonErrorCode.InspectionFailed);

    private static readonly OperationError TargetInvalidError =
        OperationError.UnprocessableInput(
            "The selected comparison target is not supported.",
            ComparisonErrorCode.TargetInvalid);

    private static readonly OperationError TargetUnavailableError =
        OperationError.NotFound(
            "The selected comparison target is no longer available.",
            ComparisonErrorCode.TargetUnavailable);

    private static readonly OperationError UnrelatedHistoryError =
        OperationError.UnprocessableInput(
            "The selected target does not share history with the current revision.",
            ComparisonErrorCode.UnrelatedHistory);

    private static readonly OperationError AmbiguousBaseError =
        OperationError.UnprocessableInput(
            "The current revision and selected target have multiple best merge bases.",
            ComparisonErrorCode.AmbiguousBase);

    private static readonly OperationError ChangedDuringPreparationError =
        OperationError.Conflict(
            "Repository facts changed while the comparison was being prepared.",
            ComparisonErrorCode.ChangedDuringPreparation);

    private readonly IGitRepositoryInspector _repositoryInspector =
        repositoryInspector ?? throw new ArgumentNullException(nameof(repositoryInspector));

    private readonly IGitComparisonTargetDiscovery _targetDiscovery =
        targetDiscovery ?? throw new ArgumentNullException(nameof(targetDiscovery));

    private readonly IGitCommandRunner _commandRunner =
        commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));

    private readonly IComparisonFileSummaryComposer _fileSummaryComposer =
        fileSummaryComposer ?? throw new ArgumentNullException(nameof(fileSummaryComposer));

    private readonly ILogger<GitComparisonPreparer> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<Result<PreparedComparison>> PrepareAsync(string? path, string? target, CancellationToken cancellationToken)
    {
        if (!IsApprovedTargetShape(target))
        {
            this._logger.LogDebug("Rejected comparison preparation for target {Target}: target shape is not approved.", target);
            return TargetInvalidError;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(ComparisonLimits.PreparationTimeout);
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        try
        {
            var repositoryResult = await this.InspectRepositoryAsync(
                path,
                startedAt,
                actionCancellation.Token);
            if (repositoryResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(repositoryResult);
            }

            var beginningRepository = repositoryResult.Data!;
            var beginningTargetsResult = await this._targetDiscovery.ListForRepositoryAsync(
                beginningRepository,
                null,
                null,
                null,
                startedAt,
                ComparisonLimits.PreparationTimeout,
                actionCancellation.Token);
            if (beginningTargetsResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(beginningTargetsResult);
            }

            var beginningTarget = beginningTargetsResult.Data!.Targets.SingleOrDefault(
                candidate => StringComparer.Ordinal.Equals(candidate.FullName, target));
            if (beginningTarget is null)
            {
                this._logger.LogDebug("Rejected comparison preparation for target {Target}: target is not in the discovered " +
                    "target set.", target);
                return TargetInvalidError;
            }

            var checkResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                ["check-ref-format", target!],
                actionCancellation.Token);
            if (checkResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(checkResult);
            }

            if (checkResult.Data!.ExitCode != 0)
            {
                this._logger.LogDebug("Rejected comparison preparation for target {Target}: target ref format is invalid.", target);
                return TargetInvalidError;
            }

            if (!HasQuietEmptyOutput(checkResult.Data))
            {
                return InspectionFailedError;
            }

            var targetRevisionResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                ["rev-parse", "--verify", target + "^{commit}"],
                actionCancellation.Token);
            if (targetRevisionResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(targetRevisionResult);
            }

            if (targetRevisionResult.Data!.ExitCode != 0)
            {
                this._logger.LogWarning("Comparison preparation for target {Target} could not resolve to a commit; the target may " +
                    "have been removed or moved during preparation.", target);
                return TargetUnavailableError;
            }

            var parsedTargetRevision = ParseSingleRevision(targetRevisionResult.Data);
            if (parsedTargetRevision.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(parsedTargetRevision);
            }

            var resolvedBeginningTarget = beginningTarget with
            {
                Revision = parsedTargetRevision.Data!,
            };

            var beginningStatusResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                StatusArguments(),
                actionCancellation.Token);
            if (beginningStatusResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(beginningStatusResult);
            }

            var parsedBeginningStatus =
                GitComparisonOutputParser.ParseWorkingTree(beginningStatusResult.Data!);
            if (parsedBeginningStatus.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(parsedBeginningStatus);
            }

            var mergeBaseResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                [
                    "merge-base",
                    "--all",
                    resolvedBeginningTarget.Revision,
                    beginningRepository.Head.Revision,
                ],
                actionCancellation.Token);
            if (mergeBaseResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(mergeBaseResult);
            }

            if (IsQuietNoMergeBase(mergeBaseResult.Data!))
            {
                this._logger.LogWarning("Comparison preparation for target {Target} found no shared history with HEAD.", target);
                return UnrelatedHistoryError;
            }

            var parsedMergeBases =
                GitComparisonOutputParser.ParseMergeBases(mergeBaseResult.Data!);
            if (parsedMergeBases.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(parsedMergeBases);
            }

            if (parsedMergeBases.Data!.Count == 0)
            {
                return UnrelatedHistoryError;
            }

            if (parsedMergeBases.Data.Count != 1)
            {
                this._logger.LogWarning("Comparison preparation for target {Target} found {MergeBaseCount} merge bases with HEAD; " +
                    "exactly one is required.", target, parsedMergeBases.Data.Count);
                return AmbiguousBaseError;
            }

            var mergeBaseRevision = parsedMergeBases.Data[0];
            var commitCountsResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                [
                    "rev-list",
                    "--left-right",
                    "--count",
                    resolvedBeginningTarget.Revision +
                    "..." +
                    beginningRepository.Head.Revision,
                    "--",
                ],
                actionCancellation.Token);
            if (commitCountsResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(commitCountsResult);
            }

            var parsedCommitCounts =
                GitComparisonOutputParser.ParseCommitCounts(commitCountsResult.Data!);
            if (parsedCommitCounts.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(parsedCommitCounts);
            }

            var committedFilesResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                [
                    "diff",
                    "--raw",
                    "-z",
                    "--no-abbrev",
                    "--full-index",
                    "--find-renames=50%",
                    "--no-ext-diff",
                    "--no-textconv",
                    mergeBaseRevision,
                    beginningRepository.Head.Revision,
                    "--",
                ],
                actionCancellation.Token);
            if (committedFilesResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(committedFilesResult);
            }

            var parsedCommittedFiles =
                GitComparisonOutputParser.ParseCommittedFiles(committedFilesResult.Data!);
            if (parsedCommittedFiles.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(parsedCommittedFiles);
            }

            var summaryResult = this._fileSummaryComposer.Compose(
                ComposeFileRecords(
                    parsedCommittedFiles.Data!,
                    parsedBeginningStatus.Data!));
            if (summaryResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(summaryResult);
            }

            var endingStatusResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                StatusArguments(),
                actionCancellation.Token);
            if (endingStatusResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(endingStatusResult);
            }

            var parsedEndingStatus =
                GitComparisonOutputParser.ParseWorkingTree(endingStatusResult.Data!);
            if (parsedEndingStatus.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(parsedEndingStatus);
            }

            var endingRevisionResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                ["rev-parse", "--verify", "HEAD^{commit}"],
                actionCancellation.Token);
            if (endingRevisionResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(endingRevisionResult);
            }

            var parsedEndingRevision = ParseSingleRevision(endingRevisionResult.Data!);
            if (parsedEndingRevision.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(parsedEndingRevision);
            }

            var endingHeadResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                ["symbolic-ref", "--quiet", "--short", "HEAD"],
                actionCancellation.Token);
            if (endingHeadResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(endingHeadResult);
            }

            var parsedEndingHead = ParseHead(
                endingHeadResult.Data!,
                parsedEndingRevision.Data!);
            if (parsedEndingHead.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(parsedEndingHead);
            }

            var endingRepository = beginningRepository with
            {
                Head = parsedEndingHead.Data!,
            };

            var endingTargetRevisionResult = await this.RunAsync(
                beginningRepository.CanonicalPath,
                startedAt,
                ["rev-parse", "--verify", target + "^{commit}"],
                actionCancellation.Token);
            if (endingTargetRevisionResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(endingTargetRevisionResult);
            }

            string? endingTargetRevision = null;
            if (endingTargetRevisionResult.Data!.ExitCode != 0 &&
                !IsConfirmedMissingTarget(endingTargetRevisionResult.Data))
            {
                return InspectionFailedError;
            }

            if (endingTargetRevisionResult.Data.ExitCode == 0)
            {
                var parsedEndingTargetRevision =
                    ParseSingleRevision(endingTargetRevisionResult.Data);
                if (parsedEndingTargetRevision.IsFailure)
                {
                    return Result.ErrorFromResult<PreparedComparison>(
                        parsedEndingTargetRevision);
                }

                endingTargetRevision = parsedEndingTargetRevision.Data!;
            }

            var endingTargetsResult = await this._targetDiscovery.ListForRepositoryAsync(
                endingRepository,
                null,
                null,
                null,
                startedAt,
                ComparisonLimits.PreparationTimeout,
                actionCancellation.Token);
            if (endingTargetsResult.IsFailure)
            {
                return Result.ErrorFromResult<PreparedComparison>(endingTargetsResult);
            }

            if (!Equals(beginningRepository.Head, endingRepository.Head) ||
                !StringComparer.Ordinal.Equals(
                    resolvedBeginningTarget.Revision,
                    endingTargetRevision) ||
                !StringComparer.Ordinal.Equals(
                    beginningTargetsResult.Data.TargetSetToken,
                    endingTargetsResult.Data!.TargetSetToken) ||
                !WorkingTreeFactsEqual(
                    parsedBeginningStatus.Data!,
                    parsedEndingStatus.Data!))
            {
                this._logger.LogWarning("Comparison preparation for target {Target} was aborted because repository facts changed " +
                    "while preparing in {ElapsedMilliseconds:0.000} ms.", target, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return ChangedDuringPreparationError;
            }

            var endingTarget = resolvedBeginningTarget with
            {
                Revision = endingTargetRevision!,
            };
            var files = summaryResult.Data!;
            var readiness = files.ConflictedFileCount > 0
                ? ComparisonReadiness.Conflicts
                : parsedCommitCounts.Data.CurrentWork == 0 && files.ChangedFileTotal == 0
                    ? ComparisonReadiness.Empty
                    : ComparisonReadiness.Ready;
            var freshnessToken = ComparisonFingerprint.CreateFreshnessToken(
                endingRepository,
                endingTarget,
                endingTargetsResult.Data.TargetSetToken,
                parsedEndingStatus.Data!);

            this._logger.LogInformation("Prepared comparison for target {Target} with readiness {Readiness}, {ChangedFileTotal} changed " +
                "files, and {CurrentWorkCommitCount} commits ahead in {ElapsedMilliseconds:0.000} ms.", target, readiness,
                files.ChangedFileTotal, parsedCommitCounts.Data.CurrentWork, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return Result.Success(
                new PreparedComparison(
                    endingRepository,
                    endingTarget,
                    mergeBaseRevision,
                    parsedCommitCounts.Data.CurrentWork,
                    parsedCommitCounts.Data.TargetOnly,
                    files,
                    readiness,
                    freshnessToken));
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            this._logger.LogWarning("Comparison preparation for target {Target} timed out after {ElapsedMilliseconds:0.000} ms.", target,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return TimedOutError;
        }
    }

    /// <summary>
    ///     Inspects the selected repository with the time remaining in the preparation action.
    /// </summary>
    /// <param name="path">The selected repository directory path.</param>
    /// <param name="startedAt">The monotonic timestamp at which preparation began.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the repository descriptor.
    /// </returns>
    private async Task<Result<RepositoryDescriptor>> InspectRepositoryAsync(
        string? path,
        long startedAt,
        CancellationToken cancellationToken)
    {
        var remaining = Remaining(startedAt);
        return remaining <= TimeSpan.Zero
            ? TimedOutError
            : await this._repositoryInspector.InspectAsync(
                path,
                remaining,
                ComparisonErrors(),
                cancellationToken);
    }

    /// <summary>
    ///     Runs one fixed comparison command with the action's remaining time and stream bounds.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository root. Cannot be <see langword="null" />.</param>
    /// <param name="startedAt">The monotonic timestamp at which preparation began.</param>
    /// <param name="subcommandArguments">
    ///     The fixed Git subcommand arguments. Cannot be <see langword="null" />.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains bounded Git output.
    /// </returns>
    private Task<Result<GitCommandOutput>> RunAsync(
        string canonicalPath,
        long startedAt,
        IReadOnlyList<string> subcommandArguments,
        CancellationToken cancellationToken)
    {
        var remaining = Remaining(startedAt);
        if (remaining <= TimeSpan.Zero)
        {
            return Task.FromResult<Result<GitCommandOutput>>(TimedOutError);
        }

        return this._commandRunner.RunAsync(
            new GitCommand(
                DirectArguments(canonicalPath, subcommandArguments),
                remaining,
                ComparisonLimits.MaximumFactOutputBytes,
                ComparisonLimits.MaximumDiagnosticBytes,
                ComparisonErrors()),
            cancellationToken);
    }

    /// <summary>
    ///     Prepends the canonical root and fixed safety configuration to a Git subcommand.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository root. Cannot be <see langword="null" />.</param>
    /// <param name="subcommandArguments">
    ///     The fixed Git subcommand arguments. Cannot be <see langword="null" />.
    /// </param>
    /// <returns>The complete separate-argument Git invocation.</returns>
    private static IReadOnlyList<string> DirectArguments(
        string canonicalPath,
        IReadOnlyList<string> subcommandArguments) =>
        [
            "-C",
            canonicalPath,
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

    /// <summary>
    ///     Creates the fixed porcelain-v2 status argument sequence used for both consistency reads.
    /// </summary>
    /// <returns>The status subcommand arguments.</returns>
    private static IReadOnlyList<string> StatusArguments() =>
        [
            "status",
            "--porcelain=v2",
            "-z",
            "--untracked-files=all",
            "--ignore-submodules=none",
            "--find-renames=50%",
        ];

    /// <summary>
    ///     Converts parsed committed and non-ignored working-tree facts into composition records.
    /// </summary>
    /// <param name="committedFiles">
    ///     The parsed committed file facts. Cannot be <see langword="null" />.
    /// </param>
    /// <param name="workingTree">
    ///     The parsed beginning working-tree facts. Cannot be <see langword="null" />.
    /// </param>
    /// <returns>The file records used for lineage and category composition.</returns>
    private static IReadOnlyList<ComparisonFileRecord> ComposeFileRecords(
        IReadOnlyList<GitComparisonFileRecord> committedFiles,
        IReadOnlyList<GitWorkingTreeRecord> workingTree)
    {
        var records = new List<ComparisonFileRecord>(
            committedFiles.Count + workingTree.Count);
        records.AddRange(
            committedFiles.Select(
                record => new ComparisonFileRecord(
                    record.Path,
                    record.OriginalPath,
                    true,
                    false,
                    false,
                    false,
                    false)));
        records.AddRange(
            workingTree
                .Where(record => !record.IsIgnored)
                .Select(
                    record => new ComparisonFileRecord(
                        record.Path,
                        record.OriginalPath,
                        false,
                        record.IsStaged,
                        record.IsUnstaged,
                        record.IsUntracked,
                        record.IsConflicted)));
        return records;
    }

    /// <summary>
    ///     Requires exactly one full revision from a successful quiet Git command.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>The parsed revision or the stable comparison inspection failure.</returns>
    private static Result<string> ParseSingleRevision(GitCommandOutput output)
    {
        var parsed = GitComparisonOutputParser.ParseMergeBases(output);
        return parsed.IsSuccess && parsed.Data!.Count == 1
            ? Result.Success<string>(parsed.Data[0])
            : parsed.IsFailure
                ? Result.ErrorFromResult<string>(parsed)
                : InspectionFailedError;
    }

    /// <summary>
    ///     Parses the ending attached or detached HEAD without exposing captured output.
    /// </summary>
    /// <param name="output">The captured symbolic-ref output. Cannot be <see langword="null" />.</param>
    /// <param name="revision">
    ///     The already parsed ending HEAD revision. Cannot be <see langword="null" /> or empty.
    /// </param>
    /// <returns>The typed ending HEAD or the stable comparison inspection failure.</returns>
    private static Result<RepositoryHead> ParseHead(
        GitCommandOutput output,
        string revision)
    {
        if (!HasBoundedValidText(output.StandardOutput) ||
            !HasBoundedValidDiagnostics(output.StandardError))
        {
            return InspectionFailedError;
        }

        if (output.ExitCode == 1 &&
            output.StandardOutput.Length == 0 &&
            output.StandardError.Length == 0)
        {
            return Result.Success<RepositoryHead>(new DetachedRepositoryHead(revision));
        }

        if (output.ExitCode != 0 || output.StandardError.Length != 0)
        {
            return InspectionFailedError;
        }

        var branchName = RemoveOneTerminalLineEnding(output.StandardOutput);
        return branchName.Length > 0 &&
               !string.IsNullOrWhiteSpace(branchName) &&
               !branchName.Contains('\r') &&
               !branchName.Contains('\n')
            ? Result.Success<RepositoryHead>(
                new BranchRepositoryHead(branchName, revision))
            : InspectionFailedError;
    }

    /// <summary>
    ///     Compares working-tree facts after applying the canonical freshness ordering.
    /// </summary>
    /// <param name="left">The beginning working-tree facts. Cannot be <see langword="null" />.</param>
    /// <param name="right">The ending working-tree facts. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> when every normalized fact is unchanged.</returns>
    private static bool WorkingTreeFactsEqual(
        IReadOnlyList<GitWorkingTreeRecord> left,
        IReadOnlyList<GitWorkingTreeRecord> right) =>
        CanonicalWorkingTree(left).SequenceEqual(CanonicalWorkingTree(right));

    /// <summary>
    ///     Orders working-tree facts exactly as the comparison freshness fingerprint does.
    /// </summary>
    /// <param name="records">The parsed working-tree facts. Cannot be <see langword="null" />.</param>
    /// <returns>The canonically ordered facts.</returns>
    private static IEnumerable<GitWorkingTreeRecord> CanonicalWorkingTree(
        IReadOnlyList<GitWorkingTreeRecord> records) =>
        records
            .OrderBy(record => record.Path, StringComparer.Ordinal)
            .ThenBy(record => record.OriginalPath is null ? 0 : 1)
            .ThenBy(record => record.OriginalPath, StringComparer.Ordinal)
            .ThenBy(record => record.IsStaged)
            .ThenBy(record => record.IsUnstaged)
            .ThenBy(record => record.IsUntracked)
            .ThenBy(record => record.IsConflicted)
            .ThenBy(record => record.IsIgnored);

    /// <summary>
    ///     Validates the target namespace, ref shape, and Unicode bound before repository I/O.
    /// </summary>
    /// <param name="target">The supplied exact target.</param>
    /// <returns><see langword="true" /> when the target can proceed to exact discovery and Git validation.</returns>
    private static bool IsApprovedTargetShape(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) ||
            target.Contains('\0') ||
            !HasAtMostScalars(target, ComparisonLimits.MaximumRefScalars) ||
            target.EndsWith('/') ||
            target.EndsWith('.') ||
            target.Contains("..", StringComparison.Ordinal) ||
            target.Contains("//", StringComparison.Ordinal) ||
            target.Contains("@{", StringComparison.Ordinal) ||
            target.Any(
                character =>
                    char.IsControl(character) ||
                    character is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\') ||
            target.Split('/').Any(
                component =>
                    component.Length == 0 ||
                    component.StartsWith('.') ||
                    component.EndsWith(".lock", StringComparison.Ordinal)))
        {
            return false;
        }

        if (target.StartsWith("refs/heads/", StringComparison.Ordinal))
        {
            return target.Length > "refs/heads/".Length;
        }

        if (!target.StartsWith("refs/remotes/", StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = target["refs/remotes/".Length..];
        return suffix.Contains('/') &&
               !suffix.StartsWith('/') &&
               !suffix.EndsWith('/');
    }

    /// <summary>
    ///     Validates Unicode scalar encoding and a maximum scalar count.
    /// </summary>
    /// <param name="value">The value to inspect. Cannot be <see langword="null" />.</param>
    /// <param name="maximum">The inclusive maximum number of Unicode scalars.</param>
    /// <returns><see langword="true" /> when the value is valid and within the bound.</returns>
    private static bool HasAtMostScalars(
        string value,
        int maximum)
    {
        var remaining = value.AsSpan();
        var count = 0;

        while (!remaining.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(
                remaining,
                out _,
                out var charactersConsumed);
            if (status != OperationStatus.Done || ++count > maximum)
            {
                return false;
            }

            remaining = remaining[charactersConsumed..];
        }

        return true;
    }

    /// <summary>
    ///     Determines whether a command succeeded without producing either stream.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> for exit code zero and two empty streams.</returns>
    private static bool HasQuietEmptyOutput(GitCommandOutput output) =>
        output.ExitCode == 0 &&
        output.StandardOutput.Length == 0 &&
        output.StandardError.Length == 0;

    /// <summary>
    ///     Recognizes only Git's quiet no-merge-base command outcome.
    /// </summary>
    /// <param name="output">The captured merge-base output. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> for exit code one and two empty streams.</returns>
    private static bool IsQuietNoMergeBase(GitCommandOutput output) =>
        output.ExitCode == 1 &&
        output.StandardOutput.Length == 0 &&
        output.StandardError.Length == 0;

    /// <summary>
    ///     Recognizes the reviewed C-locale diagnostic for an exact target that no longer exists.
    /// </summary>
    /// <param name="output">
    ///     The captured ending target-resolution output. Cannot be <see langword="null" />.
    /// </param>
    /// <returns><see langword="true" /> only for the bounded, exact missing-revision outcome.</returns>
    private static bool IsConfirmedMissingTarget(GitCommandOutput output)
    {
        if (output.ExitCode != 128 ||
            output.StandardOutput.Length != 0 ||
            !HasBoundedValidDiagnostics(output.StandardError))
        {
            return false;
        }

        var diagnostic = RemoveOneTerminalLineEnding(output.StandardError);
        return diagnostic.Equals(
            "fatal: Needed a single revision",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates decoded fact text against UTF-8 and the comparison fact bound.
    /// </summary>
    /// <param name="value">The decoded fact text. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the text is valid and bounded.</returns>
    private static bool HasBoundedValidText(string value)
    {
        try
        {
            return StrictUtf8.GetByteCount(value) <=
                   ComparisonLimits.MaximumFactOutputBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Validates decoded diagnostic text against UTF-8 and the diagnostic bound.
    /// </summary>
    /// <param name="value">The decoded diagnostic text. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the text is valid and bounded.</returns>
    private static bool HasBoundedValidDiagnostics(string value)
    {
        try
        {
            return StrictUtf8.GetByteCount(value) <=
                   ComparisonLimits.MaximumDiagnosticBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Removes one optional LF or CRLF while preserving every other character.
    /// </summary>
    /// <param name="value">The captured text. Cannot be <see langword="null" />.</param>
    /// <returns>The text without one optional terminal line ending.</returns>
    private static string RemoveOneTerminalLineEnding(string value) =>
        value.EndsWith("\r\n", StringComparison.Ordinal)
            ? value[..^2]
            : value.EndsWith('\n')
                ? value[..^1]
                : value;

    /// <summary>
    ///     Calculates the time remaining in the single preparation budget.
    /// </summary>
    /// <param name="startedAt">The monotonic timestamp at which preparation began.</param>
    /// <returns>The remaining duration, which can be nonpositive.</returns>
    private static TimeSpan Remaining(long startedAt) =>
        ComparisonLimits.PreparationTimeout - Stopwatch.GetElapsedTime(startedAt);

    /// <summary>
    ///     Creates the immutable terminal-error policy for one comparison command.
    /// </summary>
    /// <returns>The comparison timeout, output-limit, and inspection errors.</returns>
    private static GitCommandErrorPolicy ComparisonErrors() =>
        new(TimedOutError, TooLargeError, InspectionFailedError);
}
