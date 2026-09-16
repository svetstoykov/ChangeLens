using System.Globalization;
using System.Text;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Constants;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Core.Snapshots.Services;

/// <summary>
///     Reads only the Git objects and revisions retained by one captured snapshot.
/// </summary>
/// <remarks>
///     This session is created by <see cref="FrozenGitTreeReaderFactory" /> and is not thread-safe. Every Git command
///     names the canonical repository and a captured revision or object identifier; the worktree is never opened.
/// </remarks>
internal sealed class FrozenGitTreeReader : IFrozenGitTreeReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IGitBinaryCommandRunner _commandRunner;
    private readonly AnalysisRepositoryIdentity _repository;
    private readonly SnapshotManifest _snapshot;
    private readonly FrozenGitTreeReaderOptions _options;
    private readonly ILogger<FrozenGitTreeReader> _logger;
    private readonly HashSet<string> _manifestObjectIds;
    private readonly HashSet<string> _treeObjectIds = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a reader for one captured repository and snapshot manifest.
    /// </summary>
    /// <param name="commandRunner">The binary-safe installed Git command runner. Cannot be <see langword="null" />.</param>
    /// <param name="repository">The accepted repository identity. Cannot be <see langword="null" />.</param>
    /// <param name="snapshot">The captured snapshot manifest. Cannot be <see langword="null" />.</param>
    /// <param name="options">The copied reader bounds. Cannot be <see langword="null" />.</param>
    /// <param name="logger">The logger for reader outcomes. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="commandRunner" />, <paramref name="repository" />, <paramref name="snapshot" />, or
    ///     <paramref name="options" />, or <paramref name="logger" /> is <see langword="null" />.
    /// </exception>
    internal FrozenGitTreeReader(
        IGitBinaryCommandRunner commandRunner,
        AnalysisRepositoryIdentity repository,
        SnapshotManifest snapshot,
        FrozenGitTreeReaderOptions options,
        ILogger<FrozenGitTreeReader> logger)
    {
        ArgumentNullException.ThrowIfNull(commandRunner);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this._commandRunner = commandRunner;
        this._repository = repository;
        this._snapshot = snapshot;
        this._logger = logger;
        this._options = new FrozenGitTreeReaderOptions
        {
            MaximumBlobBytes = options.MaximumBlobBytes,
            MaximumTreeFiles = options.MaximumTreeFiles,
            MaximumHistoryCommits = options.MaximumHistoryCommits,
            MaximumHistoryPathsPerCommit = options.MaximumHistoryPathsPerCommit,
            CommandTimeout = options.CommandTimeout,
        };
        this._manifestObjectIds = snapshot.Entries
            .SelectMany(static entry => new[] { entry.MergeBaseObjectId, entry.HeadObjectId })
            .Where(static objectId => !IsAbsentObjectId(objectId))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<Result<FrozenGitTreeListing>> ListTreeAsync(CancellationToken cancellationToken)
    {
        var outputResult = await this.RunAsync(
            ["ls-tree", "-r", "-z", "--full-tree", "--long", this._snapshot.HeadRevision, "--"],
            TreeOutputBytes(),
            cancellationToken);
        if (outputResult.IsFailure)
        {
            return Result.ErrorFromResult<FrozenGitTreeListing>(outputResult);
        }

        if (outputResult.Data!.ExitCode != 0)
        {
            return this.StaleRevision<FrozenGitTreeListing>("list-tree", this._snapshot.HeadRevision);
        }

        if (!TryDecode(outputResult.Data.StandardOutput, out var text))
        {
            return this.ReadFailed<FrozenGitTreeListing>("list-tree", "The captured Git tree listing was not valid UTF-8.");
        }

        var files = new List<FrozenGitTreeFile>();
        var truncated = false;
        foreach (var field in text.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseTreeFile(field, out var file))
            {
                continue;
            }

            if (files.Count >= this._options.MaximumTreeFiles)
            {
                truncated = true;
                continue;
            }

            files.Add(file);
            this._treeObjectIds.Add(file.ObjectId);
        }

        return Result.Success(new FrozenGitTreeListing(files, truncated));
    }

    /// <inheritdoc />
    public async Task<Result<FrozenGitBlob>> ReadBlobAsync(string objectId, CancellationToken cancellationToken)
    {
        if (!this.IsAllowedTreeObject(objectId))
        {
            return this.ObjectNotCaptured<FrozenGitBlob>("read-blob", objectId);
        }

        return await this.ReadBlobCoreAsync(objectId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<FrozenGitBlobDiff>> ReadBlobDiffAsync(
        SnapshotManifestEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!this._snapshot.Entries.Contains(entry))
        {
            return Result.Fail<FrozenGitBlobDiff>(
                OperationError.Validation("The manifest entry is not part of this captured snapshot.", SnapshotErrorCode.InvalidSnapshot));
        }

        var beforeObjectId = IsAbsentObjectId(entry.MergeBaseObjectId) ? null : entry.MergeBaseObjectId;
        var afterObjectId = IsAbsentObjectId(entry.HeadObjectId) ? null : entry.HeadObjectId;
        if (beforeObjectId is null && afterObjectId is null)
        {
            return this.ReadFailed<FrozenGitBlobDiff>("read-blob-diff", "The captured manifest entry has no readable blob identity.");
        }

        FrozenGitBlob? before = null;
        if (beforeObjectId is not null)
        {
            var beforeResult = await this.ReadBlobCoreAsync(beforeObjectId, cancellationToken);
            if (beforeResult.IsFailure)
            {
                return Result.ErrorFromResult<FrozenGitBlobDiff>(beforeResult);
            }

            before = beforeResult.Data;
        }

        FrozenGitBlob? after = null;
        if (afterObjectId is not null)
        {
            var afterResult = await this.ReadBlobCoreAsync(afterObjectId, cancellationToken);
            if (afterResult.IsFailure)
            {
                return Result.ErrorFromResult<FrozenGitBlobDiff>(afterResult);
            }

            after = afterResult.Data;
        }

        var skipReason = before?.SkipReason is not null and not FrozenGitBlobSkipReason.None
            ? before.SkipReason
            : after?.SkipReason ?? FrozenGitBlobSkipReason.None;
        if (skipReason is not FrozenGitBlobSkipReason.None)
        {
            return Result.Success(new FrozenGitBlobDiff([], [], skipReason));
        }

        if (before is null)
        {
            return Result.Success(new FrozenGitBlobDiff(NumberEveryLine(after!.Lines), [], FrozenGitBlobSkipReason.None));
        }

        if (after is null)
        {
            return Result.Success(new FrozenGitBlobDiff([], NumberEveryLine(before.Lines), FrozenGitBlobSkipReason.None));
        }

        var outputResult = await this.RunAsync(
            ["diff", "--no-ext-diff", "--no-textconv", "--no-color", "--unified=0", "--no-renames", beforeObjectId!, afterObjectId!, "--"],
            DiffOutputBytes(),
            cancellationToken);
        if (outputResult.IsFailure)
        {
            return Result.ErrorFromResult<FrozenGitBlobDiff>(outputResult);
        }

        if (outputResult.Data!.ExitCode is not (0 or 1))
        {
            return this.StaleObjectPair<FrozenGitBlobDiff>("read-blob-diff", beforeObjectId!, afterObjectId!);
        }

        if (!TryDecode(outputResult.Data.StandardOutput, out var patch))
        {
            this.LogSkipped("read-blob-diff", FrozenGitBlobSkipReason.Binary);
            return Result.Success(new FrozenGitBlobDiff([], [], FrozenGitBlobSkipReason.Binary));
        }

        return Result.Success(ParseDiff(patch));
    }

    /// <inheritdoc />
    public async Task<Result<FrozenGitHistoryScan>> ReadHistoryAsync(CancellationToken cancellationToken)
    {
        var requestedCount = this._options.MaximumHistoryCommits == int.MaxValue
            ? int.MaxValue
            : this._options.MaximumHistoryCommits + 1;
        var commitsResult = await this.RunAsync(
            ["rev-list", "--first-parent", $"--max-count={requestedCount}", this._snapshot.MergeBaseRevision],
            HistoryOutputBytes(),
            cancellationToken);
        if (commitsResult.IsFailure)
        {
            return Result.ErrorFromResult<FrozenGitHistoryScan>(commitsResult);
        }

        if (commitsResult.Data!.ExitCode != 0)
        {
            return this.StaleRevision<FrozenGitHistoryScan>("rev-list", this._snapshot.MergeBaseRevision);
        }

        if (!TryDecode(commitsResult.Data.StandardOutput, out var commitText))
        {
            return this.ReadFailed<FrozenGitHistoryScan>("rev-list", "The captured Git history was not valid UTF-8.");
        }

        var commitIds = commitText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var inspectedCount = Math.Min(commitIds.Length, this._options.MaximumHistoryCommits);
        var history = new List<FrozenGitHistoricalCommit>();
        var oversized = 0;
        for (var ordinal = 0; ordinal < inspectedCount; ordinal++)
        {
            var parentResult = await this.RunAsync(
                ["rev-list", "--parents", "--max-count=1", commitIds[ordinal]],
                HistoryOutputBytes(),
                cancellationToken);
            if (parentResult.IsFailure)
            {
                return Result.ErrorFromResult<FrozenGitHistoryScan>(parentResult);
            }

            if (parentResult.Data!.ExitCode != 0)
            {
                return this.StaleRevision<FrozenGitHistoryScan>("rev-list-parents", commitIds[ordinal]);
            }

            if (!TryDecode(parentResult.Data.StandardOutput, out var parentsText))
            {
                return this.ReadFailed<FrozenGitHistoryScan>("rev-list-parents", "The captured Git parent list was not valid UTF-8.");
            }

            var parentIds = parentsText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parentIds.Length < 2)
            {
                continue;
            }

            var changesResult = await this.RunAsync(
                ["diff-tree", "--no-commit-id", "--name-status", "-z", "-r", "--find-renames=50%", parentIds[1], commitIds[ordinal], "--"],
                HistoryOutputBytes(),
                cancellationToken,
                HistoryCommandErrors());
            if (changesResult.IsFailure)
            {
                if (changesResult.Errors.Contains(HistoryOutputLimitError))
                {
                    oversized++;
                    continue;
                }

                return Result.ErrorFromResult<FrozenGitHistoryScan>(changesResult);
            }

            if (changesResult.Data!.ExitCode != 0)
            {
                return this.StaleRevision<FrozenGitHistoryScan>("diff-tree", commitIds[ordinal]);
            }

            if (!TryDecode(changesResult.Data.StandardOutput, out var changesText)
                || !TryParseHistoryPaths(changesText, this._options.MaximumHistoryPathsPerCommit, out var paths, out var isOversized))
            {
                return this.ReadFailed<FrozenGitHistoryScan>("diff-tree", "The captured Git history changes could not be parsed.");
            }

            if (isOversized)
            {
                this.LogBoundExceeded("diff-tree");
                oversized++;
                continue;
            }

            if (paths.Count > 0)
            {
                history.Add(new FrozenGitHistoricalCommit(ordinal, paths));
            }
        }

        return Result.Success(new FrozenGitHistoryScan(history, inspectedCount, oversized, commitIds.Length > inspectedCount));
    }

    private async Task<Result<FrozenGitBlob>> ReadBlobCoreAsync(string objectId, CancellationToken cancellationToken)
    {
        var sizeResult = await this.RunAsync(["cat-file", "-s", objectId], 128, cancellationToken);
        if (sizeResult.IsFailure)
        {
            return Result.ErrorFromResult<FrozenGitBlob>(sizeResult);
        }

        if (sizeResult.Data!.ExitCode != 0
            || !TryDecode(sizeResult.Data.StandardOutput, out var sizeText)
            || !long.TryParse(sizeText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var size))
        {
            return this.StaleObject<FrozenGitBlob>("cat-file-size", objectId);
        }

        if (size > this._options.MaximumBlobBytes)
        {
            this.LogBoundExceeded("cat-file-size");
            return Result.Success(new FrozenGitBlob(objectId, [], FrozenGitBlobSkipReason.TooLarge));
        }

        var blobOutputBytes = this._options.MaximumBlobBytes == int.MaxValue
            ? int.MaxValue
            : this._options.MaximumBlobBytes + 1;
        var outputResult = await this.RunAsync(["cat-file", "blob", objectId], blobOutputBytes, cancellationToken);
        if (outputResult.IsFailure)
        {
            if (outputResult.Errors.Contains(ReadOutputLimitError))
            {
                return Result.Success(new FrozenGitBlob(objectId, [], FrozenGitBlobSkipReason.TooLarge));
            }

            return Result.ErrorFromResult<FrozenGitBlob>(outputResult);
        }

        if (outputResult.Data!.ExitCode != 0)
        {
            return this.StaleObject<FrozenGitBlob>("cat-file", objectId);
        }

        var bytes = outputResult.Data.StandardOutput;
        if (bytes.Length > this._options.MaximumBlobBytes || IsBinary(bytes))
        {
            if (bytes.Length > this._options.MaximumBlobBytes)
            {
                this.LogBoundExceeded("cat-file");
            }

            return Result.Success(new FrozenGitBlob(objectId, [], bytes.Length > this._options.MaximumBlobBytes
                ? FrozenGitBlobSkipReason.TooLarge
                : FrozenGitBlobSkipReason.Binary));
        }

        if (!TryDecode(bytes, out var text))
        {
            this.LogSkipped("cat-file", FrozenGitBlobSkipReason.Binary);
            return Result.Success(new FrozenGitBlob(objectId, [], FrozenGitBlobSkipReason.Binary));
        }

        return Result.Success(new FrozenGitBlob(objectId, SplitLines(text), FrozenGitBlobSkipReason.None));
    }

    private bool IsAllowedTreeObject(string objectId) =>
        !string.IsNullOrWhiteSpace(objectId)
        && (this._manifestObjectIds.Contains(objectId) || this._treeObjectIds.Contains(objectId));

    private async Task<Result<GitBinaryCommandOutput>> RunAsync(
        IReadOnlyList<string> arguments,
        int maximumStandardOutputBytes,
        CancellationToken cancellationToken,
        GitCommandErrorPolicy? errorPolicy = null)
    {
        var result = await this._commandRunner.RunBinaryAsync(
            new GitCommand(
                ["--no-replace-objects", "-C", this._repository.CanonicalPath, .. arguments],
                this._options.CommandTimeout,
                maximumStandardOutputBytes,
                64 * 1024,
                errorPolicy ?? CommandErrors()),
            cancellationToken);
        return result;
    }

    private Result<T> ReadFailed<T>(string operation, string message) =>
        this.LogFailure<T>(operation, OperationError.ExternalDependencyFailure(message, SnapshotErrorCode.ReadFailed));

    private Result<T> StaleRevision<T>(string operation, string revision) =>
        this.LogFailure<T>(operation, OperationError.Conflict(
            $"The captured Git revision '{revision}' is unavailable. Re-run the analysis to capture a new snapshot.",
            SnapshotErrorCode.StaleObject));

    private Result<T> StaleObject<T>(string operation, string objectId) =>
        this.LogFailure<T>(operation, OperationError.Conflict(
            $"The captured Git object '{objectId}' is unavailable. Re-run the analysis to capture a new snapshot.",
            SnapshotErrorCode.StaleObject));

    private Result<T> StaleObjectPair<T>(string operation, string beforeObjectId, string afterObjectId) =>
        this.LogFailure<T>(operation, OperationError.Conflict(
            $"The captured Git objects '{beforeObjectId}' and '{afterObjectId}' are unavailable. Re-run the analysis to capture a new snapshot.",
            SnapshotErrorCode.StaleObject));

    private Result<T> ObjectNotCaptured<T>(string operation, string objectId) =>
        this.LogFailure<T>(operation, OperationError.Validation(
            $"The Git object '{objectId}' is not part of the captured snapshot.", SnapshotErrorCode.ObjectNotCaptured));

    private Result<T> LogFailure<T>(string operation, OperationError error)
    {
        this._logger.LogWarning("Frozen Git snapshot operation {Operation} returned error code {ErrorCode}.", operation, error.Code);
        return error;
    }

    private void LogBoundExceeded(string operation) => this._logger.LogWarning(
        "Frozen Git snapshot operation {Operation} exceeded its configured bound with error code {ErrorCode}.",
        operation, SnapshotErrorCode.ReadFailed);

    private void LogSkipped(string operation, FrozenGitBlobSkipReason reason) => this._logger.LogWarning(
        "Frozen Git snapshot operation {Operation} skipped content with reason {SkipReason} and error code {ErrorCode}.",
        operation, reason, SnapshotErrorCode.ReadFailed);

    private static GitCommandErrorPolicy CommandErrors() => new(
        OperationError.Timeout("Frozen Git access exceeded its allowed time.", SnapshotErrorCode.ReadFailed),
        ReadOutputLimitError,
        OperationError.ExternalDependencyFailure("Frozen Git access failed.", SnapshotErrorCode.ReadFailed));

    private static GitCommandErrorPolicy HistoryCommandErrors() => new(
        OperationError.Timeout("Frozen Git history access exceeded its allowed time.", SnapshotErrorCode.ReadFailed),
        HistoryOutputLimitError,
        OperationError.ExternalDependencyFailure("Frozen Git history access failed.", SnapshotErrorCode.ReadFailed));

    private static bool TryParseTreeFile(string field, out FrozenGitTreeFile file)
    {
        file = null!;
        var separator = field.IndexOf('\t');
        if (separator < 0)
        {
            return false;
        }

        var header = field[..separator].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (header.Length != 4
            || header[1] != "blob"
            || !long.TryParse(header[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
            || !IsObjectId(header[2]))
        {
            return false;
        }

        var path = field[(separator + 1)..];
        if (path.Length == 0)
        {
            return false;
        }

        file = new FrozenGitTreeFile(path, header[2], size, header[0]);
        return true;
    }

    private static bool TryParseHistoryPaths(
        string text,
        int maximumPaths,
        out IReadOnlySet<string> paths,
        out bool oversized)
    {
        var fields = text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var result = new HashSet<string>(StringComparer.Ordinal);
        oversized = false;
        for (var index = 0; index < fields.Length; index++)
        {
            var status = fields[index];
            if (status.Length == 0 || index + 1 >= fields.Length)
            {
                paths = result;
                return false;
            }

            result.Add(fields[++index]);
            if (status[0] is 'R' or 'C')
            {
                if (index + 1 >= fields.Length)
                {
                    paths = result;
                    return false;
                }

                result.Add(fields[++index]);
            }

            if (result.Count > maximumPaths)
            {
                oversized = true;
                paths = result;
                return true;
            }
        }

        paths = result;
        return true;
    }

    private static FrozenGitBlobDiff ParseDiff(string patch)
    {
        var added = new List<FrozenGitDiffLine>();
        var removed = new List<FrozenGitDiffLine>();
        var oldLine = 0;
        var newLine = 0;
        foreach (var line in SplitLines(patch))
        {
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                (oldLine, newLine) = ParseHunkHeader(line);
                continue;
            }

            if (oldLine == 0 && newLine == 0)
            {
                continue;
            }

            if (line.StartsWith('+'))
            {
                added.Add(new FrozenGitDiffLine(newLine++, line[1..]));
            }
            else if (line.StartsWith('-'))
            {
                removed.Add(new FrozenGitDiffLine(oldLine++, line[1..]));
            }
            else if (!line.StartsWith('\\'))
            {
                oldLine++;
                newLine++;
            }
        }

        return new FrozenGitBlobDiff(added, removed, FrozenGitBlobSkipReason.None);
    }

    private static (int OldLine, int NewLine) ParseHunkHeader(string header)
    {
        var oldStart = 1;
        var newStart = 1;
        foreach (var part in header.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length < 2 || (part[0] != '-' && part[0] != '+'))
            {
                continue;
            }

            var digits = part[1..].Split(',')[0];
            if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start))
            {
                continue;
            }

            if (part[0] == '-')
            {
                oldStart = Math.Max(start, 1);
            }
            else
            {
                newStart = Math.Max(start, 1);
            }
        }

        return (oldStart, newStart);
    }

    private static IReadOnlyList<FrozenGitDiffLine> NumberEveryLine(IReadOnlyList<string> lines) =>
        lines.Select((line, index) => new FrozenGitDiffLine(index + 1, line)).ToArray();

    private static string[] SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Split('\n');
        var count = lines.Length > 1 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        var result = new string[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = lines[index].EndsWith('\r') ? lines[index][..^1] : lines[index];
        }

        return result;
    }

    private static bool IsBinary(byte[] bytes) =>
        bytes.Any(static value => value == 0);

    private static bool TryDecode(byte[] bytes, out string text)
    {
        try
        {
            text = StrictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static bool IsAbsentObjectId(string objectId) =>
        string.IsNullOrWhiteSpace(objectId) || objectId.All(static character => character == '0');

    private static bool IsObjectId(string value) =>
        value.Length is 40 or 64 && value.All(static character => Uri.IsHexDigit(character));

    private static int TreeOutputBytes() => 128 * 1024 * 1024;

    private static int DiffOutputBytes() => 16 * 1024 * 1024;

    private static int HistoryOutputBytes() => 16 * 1024 * 1024;

    private static readonly OperationError ReadOutputLimitError = OperationError.UnprocessableInput(
        "Frozen Git output exceeded the configured bound.", SnapshotErrorCode.ReadFailed);

    private static readonly OperationError HistoryOutputLimitError = OperationError.UnprocessableInput(
        "Frozen Git history commit exceeded the configured path bound.", SnapshotErrorCode.ReadFailed);
}
