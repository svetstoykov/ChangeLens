using System.Diagnostics;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ChangeAnatomy.Helpers;
using ChangeLens.Core.ChangeAnatomy.Interfaces;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;
using Microsoft.Extensions.Logging;
using ChangeAnatomyModel = ChangeLens.Core.ChangeAnatomy.Models.ChangeAnatomy;

namespace ChangeLens.Core.ChangeAnatomy.Services;

/// <summary>
///     Extracts language-agnostic keys from changed lines in a frozen snapshot.
/// </summary>
/// <remarks>
///     <para>
///         The Engine registers this service as scoped. It serves one analysis request and does not need to be
///         thread-safe.
///     </para>
///     <para>
///         The service creates a snapshot-scoped reader for every invocation. It reads complete captured blob sides
///         only to preserve lexical state, while emitting keys only for changed lines.
///     </para>
/// </remarks>
/// <param name="readerFactory">The factory that opens the frozen-tree reader for the snapshot. Cannot be <see langword="null" />.</param>
/// <param name="options">The bounded key-extraction options. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for analysis flow and outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="readerFactory" />, <paramref name="options" />, or <paramref name="logger" /> is
///     <see langword="null" />.
/// </exception>
public sealed class ChangeAnatomyService(
    IFrozenGitTreeReaderFactory readerFactory,
    ChangeAnatomyOptions options,
    ILogger<ChangeAnatomyService> logger) : IChangeAnatomyService
{
    private readonly IFrozenGitTreeReaderFactory _readerFactory = readerFactory ?? throw new ArgumentNullException(nameof(readerFactory));
    private readonly ChangeAnatomyOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<ChangeAnatomyService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<Result<ChangeAnatomyModel>> AnalyzeAsync(
        AnalysisRepositoryIdentity repository,
        SnapshotManifest snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateOptions(this._options);
        cancellationToken.ThrowIfCancellationRequested();
        var started = Stopwatch.GetTimestamp();
        var readerResult = this._readerFactory.Open(repository, snapshot);
        if (readerResult.IsFailure)
        {
            return Result.ErrorFromResult<ChangeAnatomyModel>(readerResult);
        }

        var reader = readerResult.Data!;
        var tokenizer = new ChangeAnatomyTokenizer(this._options.MinimumKeyLength);
        var files = new List<ChangedFileAnatomy>(snapshot.Entries.Count);
        foreach (var entry in snapshot.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileResult = await this.AnalyzeFileAsync(reader, tokenizer, entry, cancellationToken);
            if (fileResult.IsFailure)
            {
                return Result.ErrorFromResult<ChangeAnatomyModel>(fileResult);
            }

            files.Add(fileResult.Data!);
        }

        var anatomy = new ChangeAnatomyModel(files, Summarize(files));
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        this._logger.LogInformation(
            "Change anatomy completed with {ChangedFileCount} changed files, {AnalyzedFileCount} analyzed, " +
            "{SkippedFileCount} skipped, {DistinctKeyCount} distinct keys, and {KeyOccurrenceCount} occurrences " +
            "in {ElapsedMilliseconds:0.000} ms.",
            anatomy.Diagnostics.ChangedFileCount,
            anatomy.Diagnostics.AnalyzedFileCount,
            anatomy.Diagnostics.SkippedFileCount,
            anatomy.Diagnostics.DistinctKeyCount,
            anatomy.Diagnostics.KeyOccurrenceCount,
            elapsed);
        if (anatomy.Diagnostics.ZeroKeyFileCount > 0)
        {
            this._logger.LogWarning(
                "Change anatomy analyzed {ZeroKeyFileCount} file(s) that yielded zero keys.",
                anatomy.Diagnostics.ZeroKeyFileCount);
        }

        if (anatomy.Diagnostics.TruncatedFileCount > 0)
        {
            this._logger.LogWarning(
                "Change anatomy truncated {TruncatedFileCount} file(s) at the configured {MaximumKeysPerFile}-key cap.",
                anatomy.Diagnostics.TruncatedFileCount,
                this._options.MaximumKeysPerFile);
        }

        return anatomy;
    }

    private async Task<Result<ChangedFileAnatomy>> AnalyzeFileAsync(
        IFrozenGitTreeReader reader,
        ChangeAnatomyTokenizer tokenizer,
        SnapshotManifestEntry entry,
        CancellationToken cancellationToken)
    {
        var exclusion = ChangeAnatomyPathRules.ExclusionReason(entry.Path)
            ?? (entry.OriginalPath is null ? null : ChangeAnatomyPathRules.ExclusionReason(entry.OriginalPath));
        if (exclusion is not null)
        {
            return Skipped(entry, exclusion);
        }

        if (entry.MergeBaseEntryMode == "160000" || entry.HeadEntryMode == "160000")
        {
            return Skipped(entry, "submodule pointer");
        }

        var diffResult = await reader.ReadBlobDiffAsync(entry, cancellationToken);
        if (diffResult.IsFailure)
        {
            return Result.ErrorFromResult<ChangedFileAnatomy>(diffResult);
        }

        var diff = diffResult.Data!;
        if (!diff.HasContent)
        {
            return Skipped(entry, DescribeBlobSkip(diff.SkipReason));
        }

        var collector = new ChangeAnatomyKeyCollector(this._options.MaximumKeysPerFile, this._options.MaximumOccurrencesPerKey);
        var pathSide = IsAbsentObjectId(entry.HeadObjectId) ? ChangeAnatomySide.Before : ChangeAnatomySide.After;
        tokenizer.TokenizePath(entry.Path, (normalized, kind, line, original) =>
            collector.Add(normalized, kind, pathSide, line, original));
        if (entry.OriginalPath is not null)
        {
            tokenizer.TokenizePath(entry.OriginalPath, (normalized, kind, line, original) =>
                collector.Add(normalized, kind, ChangeAnatomySide.Before, line, original));
        }

        var beforeResult = await this.TokenizeSideAsync(
            reader,
            tokenizer,
            collector,
            entry.MergeBaseObjectId,
            entry.OriginalPath ?? entry.Path,
            ChangeAnatomySide.Before,
            diff.RemovedLines,
            cancellationToken);
        if (beforeResult.IsFailure)
        {
            return Result.ErrorFromResult<ChangedFileAnatomy>(beforeResult);
        }

        if (beforeResult.Data is not null)
        {
            return Skipped(entry, beforeResult.Data);
        }

        var afterResult = await this.TokenizeSideAsync(
            reader,
            tokenizer,
            collector,
            entry.HeadObjectId,
            entry.Path,
            ChangeAnatomySide.After,
            diff.AddedLines,
            cancellationToken);
        if (afterResult.IsFailure)
        {
            return Result.ErrorFromResult<ChangedFileAnatomy>(afterResult);
        }

        if (afterResult.Data is not null)
        {
            return Skipped(entry, afterResult.Data);
        }

        return new ChangedFileAnatomy(entry.Path, entry.OriginalPath, entry.Category, collector.Build(), collector.Truncated, null);
    }

    private async Task<Result<string?>> TokenizeSideAsync(
        IFrozenGitTreeReader reader,
        ChangeAnatomyTokenizer tokenizer,
        ChangeAnatomyKeyCollector collector,
        string objectId,
        string path,
        ChangeAnatomySide side,
        IReadOnlyList<FrozenGitDiffLine> changedLines,
        CancellationToken cancellationToken)
    {
        if (IsAbsentObjectId(objectId) || changedLines.Count == 0)
        {
            return Result.Success<string?>(null);
        }

        var blobResult = await reader.ReadBlobAsync(objectId, cancellationToken);
        if (blobResult.IsFailure)
        {
            return Result.ErrorFromResult<string?>(blobResult);
        }

        var blob = blobResult.Data!;
        if (!blob.HasText)
        {
            return DescribeBlobSkip(blob.SkipReason);
        }

        var changedLineNumbers = changedLines.Select(line => line.LineNumber).ToHashSet();
        var state = ChangeAnatomyLexicalState.ForPath(path);
        for (var index = 0; index < blob.Lines.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lineNumber = index + 1;
            tokenizer.TokenizeLine(blob.Lines[index], lineNumber, ref state, (normalized, kind, line, original) =>
            {
                if (line is null || changedLineNumbers.Contains(line.Value))
                {
                    collector.Add(normalized, kind, side, line, original);
                }
            }, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Result.Success<string?>(null);
    }

    private static Result<ChangedFileAnatomy> Skipped(SnapshotManifestEntry entry, string reason) =>
        new ChangedFileAnatomy(entry.Path, entry.OriginalPath, entry.Category, [], false, reason);

    private static string DescribeBlobSkip(FrozenGitBlobSkipReason reason) => reason switch
    {
        FrozenGitBlobSkipReason.Binary => "binary content",
        FrozenGitBlobSkipReason.TooLarge => "larger than the configured blob bound",
        _ => "unreadable content",
    };

    private static ChangeAnatomyDiagnostics Summarize(IReadOnlyList<ChangedFileAnatomy> files)
    {
        var distinct = new HashSet<(string Normalized, ChangeAnatomyKeyKind Kind)>();
        var occurrences = 0;
        var analyzed = 0;
        var truncated = 0;
        var zeroKeys = 0;
        var skipReasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (file.IsAnalyzed)
            {
                analyzed++;
                if (file.HasZeroKeys)
                {
                    zeroKeys++;
                }
            }

            if (file.Truncated)
            {
                truncated++;
            }

            if (file.SkipReason is not null)
            {
                skipReasons[file.SkipReason] = skipReasons.TryGetValue(file.SkipReason, out var count) ? count + 1 : 1;
            }

            foreach (var key in file.Keys)
            {
                distinct.Add((key.Normalized, key.Kind));
                occurrences += key.Occurrences.Count;
            }
        }

        var byKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, kind) in distinct)
        {
            var name = kind.ToString();
            byKind[name] = byKind.TryGetValue(name, out var count) ? count + 1 : 1;
        }

        return new ChangeAnatomyDiagnostics(
            files.Count,
            analyzed,
            files.Count - analyzed,
            truncated,
            zeroKeys,
            distinct.Count,
            occurrences,
            byKind,
            skipReasons);
    }

    private static bool IsAbsentObjectId(string objectId) =>
        string.IsNullOrWhiteSpace(objectId) || objectId.All(static character => character == '0');

    private static void ValidateOptions(ChangeAnatomyOptions options)
    {
        if (options.MinimumKeyLength <= 0 || options.MaximumKeysPerFile <= 0 || options.MaximumOccurrencesPerKey <= 0)
        {
            throw new InvalidOperationException("Change anatomy options must contain positive bounds.");
        }
    }
}
