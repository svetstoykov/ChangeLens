using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ChangeAnatomy.Helpers;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.EvidenceGraph.Helpers;
using ChangeLens.Core.EvidenceGraph.Interfaces;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;
using Microsoft.Extensions.Logging;
using ChangeAnatomyModel = ChangeLens.Core.ChangeAnatomy.Models.ChangeAnatomy;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.EvidenceGraph.Services;

/// <summary>
///     Builds the bounded evidence graph of changed hunks, manifest facts, candidate windows, and match edges.
/// </summary>
/// <remarks>
///     <para>
///         The Engine registers this service as scoped. It serves one analysis request and does not need to be
///         thread-safe.
///     </para>
///     <para>
///         Every quote comes from a frozen blob read through one snapshot-scoped reader. The worktree is never opened,
///         and a blob is read at most once per build.
///     </para>
///     <para>
///         A manifest fact, such as a rename or a mode change, quotes no lines, so it takes no quote-window slot and is
///         always kept. Quote windows compete for the configured node budget.
///     </para>
/// </remarks>
/// <param name="readerFactory">The factory that opens the frozen-tree reader for the snapshot. Cannot be <see langword="null" />.</param>
/// <param name="options">The evidence graph bounds. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for graph flow and outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="readerFactory" />, <paramref name="options" />, or <paramref name="logger" /> is
///     <see langword="null" />.
/// </exception>
public sealed class EvidenceGraphService(
    IFrozenGitTreeReaderFactory readerFactory,
    EvidenceGraphOptions options,
    ILogger<EvidenceGraphService> logger) : IEvidenceGraphService
{
    private readonly IFrozenGitTreeReaderFactory _readerFactory = readerFactory ?? throw new ArgumentNullException(nameof(readerFactory));
    private readonly EvidenceGraphOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<EvidenceGraphService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<Result<EvidenceGraphModel>> BuildAsync(
        AnalysisRepositoryIdentity repository,
        SnapshotManifest snapshot,
        ChangeAnatomyModel anatomy,
        CorrespondenceRanking ranking,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(anatomy);
        ArgumentNullException.ThrowIfNull(ranking);
        ValidateOptions(this._options);
        cancellationToken.ThrowIfCancellationRequested();
        var started = Stopwatch.GetTimestamp();
        var skipReasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var analyzed = anatomy.Files.Where(file => file.IsAnalyzed).ToArray();
        if (snapshot.Entries.Count == 0 && ranking.Candidates.Count == 0)
        {
            var empty = CreateEmptyGraph(analyzed.Length, ranking.Candidates.Count, skipReasons);
            this.LogCompletion(empty.Diagnostics, started);
            return empty;
        }

        var readerResult = this._readerFactory.Open(repository, snapshot);
        if (readerResult.IsFailure)
        {
            return Result.ErrorFromResult<EvidenceGraphModel>(readerResult);
        }

        var reader = readerResult.Data!;
        if (ranking.Candidates.Count > 0)
        {
            var listingResult = await reader.ListTreeAsync(cancellationToken);
            if (listingResult.IsFailure)
            {
                return Result.ErrorFromResult<EvidenceGraphModel>(listingResult);
            }
        }

        var blobs = new EvidenceGraphBlobCache(reader, skipReasons);
        var changedFiles = await this.CollectChangedFilesAsync(snapshot, reader, blobs, skipReasons, cancellationToken);
        if (changedFiles.Failure is not null)
        {
            return Result.ErrorFromResult<EvidenceGraphModel>(changedFiles.Failure);
        }

        var changedByPath = changedFiles.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var (factNodes, factByPath) = BuildManifestFacts(snapshot, changedByPath);
        var candidates = await this.CollectCandidateWindowsAsync(ranking, blobs, cancellationToken);
        if (candidates.Failure is not null)
        {
            return Result.ErrorFromResult<EvidenceGraphModel>(candidates.Failure);
        }

        var candidateIndexByPath = ranking.Candidates
            .Select((candidate, index) => (candidate.Path, Index: index))
            .ToDictionary(pair => pair.Path, pair => pair.Index, StringComparer.Ordinal);
        var rawWindows = changedFiles.Files.SelectMany(file => file.Windows).Concat(candidates.AllWindows).ToArray();
        var (mergedWindows, mergeCount) = SplitAndMerge(
            rawWindows, this._options.MaximumQuotedLinesPerNode, this._options.MergeGapLines);
        var mergedCandidateWindows = RegroupWindows(mergedWindows, changedByPath, candidateIndexByPath, ranking.Candidates.Count);
        var changedBudget = Math.Clamp(
            (int)Math.Ceiling(this._options.MaximumQuoteWindowNodes * this._options.ChangedNodeShare),
            0,
            this._options.MaximumQuoteWindowNodes);
        var candidateBudget = this._options.MaximumQuoteWindowNodes - changedBudget;
        var selection = this.SelectWindows(changedFiles.Files, mergedCandidateWindows, changedBudget, candidateBudget);
        var (nodes, changedWindowNodesByPath, candidateNodesByIndex) =
            BuildNodes(factNodes, selection, blobs);
        var pendingEdges = BuildPendingEdges(
            anatomy, ranking, changedWindowNodesByPath, factByPath, candidateNodesByIndex, candidates.ContributionsByIndex, skipReasons);
        var edgeSelection = SelectEdges(pendingEdges, this._options.MaximumEdges, this._options.MaximumEdgesPerNodePair);
        var representedPaths = changedWindowNodesByPath.Keys.Concat(factByPath.Keys).ToHashSet(StringComparer.Ordinal);
        var diagnostics = new EvidenceGraphDiagnostics(
            nodes.Count,
            factNodes.Count,
            edgeSelection.Edges.Count,
            QuotedLineCount(nodes),
            mergeCount,
            analyzed.Length,
            representedPaths.Count,
            ranking.Candidates.Count,
            candidateNodesByIndex.Count,
            candidates.WithoutQuotableLine,
            nodes.Count(node => node.Truncated),
            mergedWindows.Count - selection.ChangedWindows.Count - selection.CandidateWindows.Count,
            changedBudget,
            candidateBudget,
            edgeSelection.PairDroppedCount,
            edgeSelection.TotalDroppedCount,
            edgeSelection.ContributionFloor,
            edgeSelection.MaxPairDroppedContribution,
            edgeSelection.PairDroppedAboveFloorCount,
            CountByOrigin(nodes),
            CountByKind(edgeSelection.Edges),
            skipReasons);
        this.LogCompletion(diagnostics, started);
        var unrepresented = analyzed.Count(file => !representedPaths.Contains(file.Path));
        if (unrepresented > 0)
        {
            this._logger.LogWarning(
                "Evidence graph left {UnrepresentedChangedFileCount} analyzed changed file(s) without a node.", unrepresented);
        }

        return new EvidenceGraphModel(nodes, edgeSelection.Edges, diagnostics);
    }

    private async Task<(List<EvidenceGraphChangedFile> Files, Result? Failure)> CollectChangedFilesAsync(
        SnapshotManifest snapshot, IFrozenGitTreeReader reader, EvidenceGraphBlobCache blobs, IDictionary<string, int> skipReasons,
        CancellationToken cancellationToken)
    {
        var files = new List<EvidenceGraphChangedFile>(snapshot.Entries.Count);
        foreach (var entry in snapshot.Entries)
        {
            if (ChangeAnatomyPathRules.ExclusionReason(entry.Path) is not null)
            {
                continue;
            }

            if (entry.OriginalPath is not null && ChangeAnatomyPathRules.ExclusionReason(entry.OriginalPath) is not null)
            {
                continue;
            }

            if (entry.MergeBaseEntryMode == "160000" || entry.HeadEntryMode == "160000")
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var file = new EvidenceGraphChangedFile(entry.Path);
            files.Add(file);
            var diffResult = await reader.ReadBlobDiffAsync(entry, cancellationToken);
            if (diffResult.IsFailure)
            {
                return (files, diffResult);
            }

            var diff = diffResult.Data!;
            if (!diff.HasContent)
            {
                EvidenceGraphBlobCache.Count(skipReasons, EvidenceGraphBlobCache.DescribeSkip(diff.SkipReason));
                continue;
            }

            if (diff.RemovedLines.Count > 0 && entry.Category is not SnapshotChangeCategory.Added)
            {
                var before = await blobs.ReadAsync(entry.MergeBaseObjectId, cancellationToken);
                if (before.IsFailure)
                {
                    return (files, before);
                }

                if (before.Data!.HasText)
                {
                    this.AddHunkWindows(
                        file, entry.OriginalPath ?? entry.Path, ChangeAnatomySide.Before, entry.MergeBaseObjectId, before.Data.Lines,
                        diff.RemovedLines);
                }
            }

            if (diff.AddedLines.Count > 0 && entry.Category is not SnapshotChangeCategory.Deleted)
            {
                var after = await blobs.ReadAsync(entry.HeadObjectId, cancellationToken);
                if (after.IsFailure)
                {
                    return (files, after);
                }

                if (after.Data!.HasText)
                {
                    this.AddHunkWindows(file, entry.Path, ChangeAnatomySide.After, entry.HeadObjectId, after.Data.Lines, diff.AddedLines);
                }
            }

            file.HasHunk = file.Windows.Count > 0;
        }

        return (files, null);
    }

    private void AddHunkWindows(
        EvidenceGraphChangedFile file, string quotePath, ChangeAnatomySide side, string objectId,
        IReadOnlyList<string> blobLines, IReadOnlyList<FrozenGitDiffLine> changedLines)
    {
        var numbers = changedLines.Select(line => line.LineNumber).OrderBy(number => number).ToArray();
        var runStart = -1;
        var runEnd = -1;
        foreach (var number in numbers)
        {
            if (runStart < 0)
            {
                runStart = number;
                runEnd = number;
                continue;
            }

            if (number == runEnd + 1)
            {
                runEnd = number;
                continue;
            }

            file.Windows.Add(this.CreateHunkWindow(quotePath, file.Path, side, objectId, blobLines.Count, runStart, runEnd));
            runStart = number;
            runEnd = number;
        }

        if (runStart >= 0)
        {
            file.Windows.Add(this.CreateHunkWindow(quotePath, file.Path, side, objectId, blobLines.Count, runStart, runEnd));
        }
    }

    private EvidenceGraphWindow CreateHunkWindow(
        string quotePath,
        string changedPath,
        ChangeAnatomySide side,
        string objectId,
        int blobLineCount,
        int runStart,
        int runEnd)
    {
        var start = Math.Max(1, runStart - this._options.HunkContextLines);
        var end = Math.Min(blobLineCount, runEnd + this._options.HunkContextLines);
        var salience = runEnd - runStart + 1;
        return new EvidenceGraphWindow(
            quotePath, changedPath, side, objectId, start, end, [EvidenceNodeOrigin.ChangedHunk], salience, false);
    }

    private static (List<EvidenceNode> Nodes, Dictionary<string, EvidenceNode> ByPath) BuildManifestFacts(
        SnapshotManifest snapshot, IReadOnlyDictionary<string, EvidenceGraphChangedFile> changedByPath)
    {
        var nodes = new List<EvidenceNode>();
        var byPath = new Dictionary<string, EvidenceNode>(StringComparer.Ordinal);
        foreach (var entry in snapshot.Entries)
        {
            if (ChangeAnatomyPathRules.ExclusionReason(entry.Path) is not null)
            {
                continue;
            }

            if (entry.OriginalPath is not null && ChangeAnatomyPathRules.ExclusionReason(entry.OriginalPath) is not null)
            {
                continue;
            }

            if (changedByPath.TryGetValue(entry.Path, out var file) && file.HasHunk)
            {
                continue;
            }

            var facts = DescribeFacts(entry);
            if (facts.Count == 0)
            {
                continue;
            }

            var deleted = entry.Category is SnapshotChangeCategory.Deleted;
            var text = $"manifest fact ({entry.Category}) — {string.Join("; ", facts)}";
            var node = new EvidenceNode(
                FactNodeId(nodes.Count + 1), entry.Path, deleted ? ChangeAnatomySide.Before : ChangeAnatomySide.After,
                deleted ? entry.MergeBaseObjectId : entry.HeadObjectId, 0, 0, text, ContentHash(text), true,
                [EvidenceNodeOrigin.Manifest], 0, false);
            nodes.Add(node);
            byPath[entry.Path] = node;
        }

        return (nodes, byPath);
    }

    private static List<string> DescribeFacts(SnapshotManifestEntry entry)
    {
        var facts = new List<string>();
        if (entry.OriginalPath is not null && !entry.OriginalPath.Equals(entry.Path, StringComparison.Ordinal))
        {
            facts.Add($"path: {entry.OriginalPath} → {entry.Path}");
        }

        if (entry.Category is SnapshotChangeCategory.Modified or SnapshotChangeCategory.Renamed or SnapshotChangeCategory.TypeChanged
            && !entry.MergeBaseEntryMode.Equals(entry.HeadEntryMode, StringComparison.Ordinal))
        {
            facts.Add($"mode: {entry.MergeBaseEntryMode} → {entry.HeadEntryMode}");
        }

        return facts;
    }

    private async Task<CandidateWindowSet> CollectCandidateWindowsAsync(
        CorrespondenceRanking ranking, EvidenceGraphBlobCache blobs, CancellationToken cancellationToken)
    {
        var byIndex = new Dictionary<int, List<EvidenceGraphWindow>>();
        var allWindows = new List<EvidenceGraphWindow>();
        var contributionsByIndex = new Dictionary<int, Dictionary<int, double>>();
        var withoutQuotableLine = 0;
        for (var index = 0; index < ranking.Candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = ranking.Candidates[index];
            var contributions = BuildLineContributions(candidate);
            contributionsByIndex[index] = contributions;
            var blobResult = await blobs.ReadAsync(candidate.ObjectId, cancellationToken);
            if (blobResult.IsFailure)
            {
                return new CandidateWindowSet(byIndex, allWindows, contributionsByIndex, withoutQuotableLine, blobResult);
            }

            var blob = blobResult.Data!;
            if (!blob.HasText)
            {
                byIndex[index] = [];
                continue;
            }

            var windows = new List<EvidenceGraphWindow>();
            if (contributions.Count == 0)
            {
                var end = Math.Max(1, Math.Min(this._options.CandidateWindowLines, blob.Lines.Count));
                windows.Add(new EvidenceGraphWindow(
                    candidate.Path, null, ChangeAnatomySide.After, candidate.ObjectId, 1, end, [EvidenceNodeOrigin.CandidateHead], 0, false));
                withoutQuotableLine++;
            }
            else
            {
                var before = (this._options.CandidateWindowLines - 1) / 2;
                var after = this._options.CandidateWindowLines / 2;
                foreach (var pair in contributions.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key))
                {
                    var existing = windows.Find(window => window.Covers(pair.Key));
                    if (existing is not null)
                    {
                        existing.Salience += pair.Value;
                        continue;
                    }

                    if (windows.Count >= this._options.MaximumNodesPerCandidate)
                    {
                        continue;
                    }

                    var start = Math.Max(1, pair.Key - before);
                    var end = Math.Min(blob.Lines.Count, pair.Key + after);
                    windows.Add(new EvidenceGraphWindow(
                        candidate.Path, null, ChangeAnatomySide.After, candidate.ObjectId, start, end,
                        [EvidenceNodeOrigin.MatchWindow], pair.Value, false));
                }
            }

            byIndex[index] = windows;
            allWindows.AddRange(windows);
        }

        return new CandidateWindowSet(byIndex, allWindows, contributionsByIndex, withoutQuotableLine, null);
    }

    private static Dictionary<int, double> BuildLineContributions(CorrespondenceCandidate candidate)
    {
        var contributions = new Dictionary<int, double>();
        foreach (var signal in candidate.Signals.OfType<SharedKeyCorrespondenceSignal>())
        {
            foreach (var line in signal.CandidateLines)
            {
                contributions[line] = contributions.GetValueOrDefault(line) + signal.Contribution;
            }
        }

        return contributions;
    }

    private static (List<EvidenceGraphWindow> Windows, int MergeCount) SplitAndMerge(
        IReadOnlyList<EvidenceGraphWindow> rawWindows, int maximumQuotedLines, int mergeGapLines)
    {
        var split = new List<EvidenceGraphWindow>();
        foreach (var window in rawWindows)
        {
            SplitWindow(split, window, maximumQuotedLines);
        }

        var merged = new List<EvidenceGraphWindow>();
        var mergeCount = 0;
        foreach (var group in split.GroupBy(window => (window.Path, window.Side, window.ObjectId)))
        {
            EvidenceGraphWindow? last = null;
            foreach (var window in group.OrderBy(candidate => candidate.Start))
            {
                if (last is not null
                    && window.Start <= last.End + mergeGapLines
                    && Math.Max(last.End, window.End) - last.Start + 1 <= maximumQuotedLines)
                {
                    last.Merge(window);
                    mergeCount++;
                    continue;
                }

                last = window;
                merged.Add(window);
            }
        }

        return (merged, mergeCount);
    }

    private static void SplitWindow(ICollection<EvidenceGraphWindow> target, EvidenceGraphWindow window, int maximumQuotedLines)
    {
        var length = window.Length;
        if (length <= maximumQuotedLines)
        {
            target.Add(window);
            return;
        }

        var chunkCount = (int)Math.Ceiling(length / (double)maximumQuotedLines);
        for (var index = 0; index < chunkCount; index++)
        {
            var start = window.Start + (index * maximumQuotedLines);
            var end = Math.Min(window.End, start + maximumQuotedLines - 1);
            var chunkLength = end - start + 1;
            var salience = window.Salience * chunkLength / length;
            target.Add(new EvidenceGraphWindow(
                window.Path, window.ChangedPath, window.Side, window.ObjectId, start, end, [.. window.Origins], salience, true));
        }
    }

    private static Dictionary<int, List<EvidenceGraphWindow>> RegroupWindows(
        IReadOnlyList<EvidenceGraphWindow> mergedWindows, IReadOnlyDictionary<string, EvidenceGraphChangedFile> changedByPath,
        IReadOnlyDictionary<string, int> candidateIndexByPath, int candidateCount)
    {
        foreach (var file in changedByPath.Values)
        {
            file.Windows.Clear();
        }

        var candidateWindows = new Dictionary<int, List<EvidenceGraphWindow>>();
        for (var index = 0; index < candidateCount; index++)
        {
            candidateWindows[index] = [];
        }

        foreach (var window in mergedWindows)
        {
            if (window.ChangedPath is not null && changedByPath.TryGetValue(window.ChangedPath, out var file))
            {
                file.Windows.Add(window);
                continue;
            }

            if (window.ChangedPath is null && candidateIndexByPath.TryGetValue(window.Path, out var candidateIndex))
            {
                candidateWindows[candidateIndex].Add(window);
            }
        }

        return candidateWindows;
    }

    private EvidenceGraphWindowSelection SelectWindows(
        IReadOnlyList<EvidenceGraphChangedFile> changedFiles, IReadOnlyDictionary<int, List<EvidenceGraphWindow>> candidateWindowsByIndex,
        int changedBudget, int candidateBudget)
    {
        foreach (var file in changedFiles)
        {
            var afterMax = file.Windows
                .Where(window => window.Side == ChangeAnatomySide.After)
                .Select(window => window.Salience)
                .DefaultIfEmpty(0)
                .Max();
            var beforeMax = file.Windows
                .Where(window => window.Side == ChangeAnatomySide.Before)
                .Select(window => window.Salience)
                .DefaultIfEmpty(0)
                .Max();
            if (beforeMax > 0 && afterMax > beforeMax)
            {
                var scale = afterMax / beforeMax;
                foreach (var window in file.Windows.Where(candidate => candidate.Side == ChangeAnatomySide.Before))
                {
                    window.Salience *= scale;
                }
            }
        }

        var orderedFiles = changedFiles
            .OrderByDescending(file => file.Windows.Select(window => window.Salience).DefaultIfEmpty(0).Max())
            .ThenBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();
        var queues = orderedFiles
            .Select(file => new EvidenceGraphChangedWindowQueue(Interleave(file.Windows), this._options.MaximumNodesPerChangedFile))
            .ToArray();
        var changedSelection = new EvidenceGraphChangedSelection(queues);
        changedSelection.Run(changedBudget);
        var changedLeftover = changedBudget - changedSelection.SelectedCount;
        var regularLimit = candidateBudget + changedLeftover;
        var regularSelected = new List<(int CandidateIndex, EvidenceGraphWindow Window)>();
        var regularQueues = candidateWindowsByIndex.ToDictionary(
            pair => pair.Key,
            pair => new Queue<EvidenceGraphWindow>(
                pair.Value
                    .Where(window => window.Origins.Contains(EvidenceNodeOrigin.MatchWindow))
                    .OrderByDescending(window => window.Salience)
                    .ThenBy(window => window.Start)));
        var regularCounts = candidateWindowsByIndex.Keys.ToDictionary(index => index, _ => 0);
        var orderedIndices = candidateWindowsByIndex.Keys.OrderBy(index => index).ToArray();
        while (regularSelected.Count < regularLimit)
        {
            var tookWindow = false;
            foreach (var index in orderedIndices)
            {
                if (regularSelected.Count >= regularLimit)
                {
                    break;
                }

                if (regularCounts[index] >= this._options.MaximumNodesPerCandidate)
                {
                    continue;
                }

                if (regularQueues[index].Count == 0)
                {
                    continue;
                }

                regularCounts[index]++;
                regularSelected.Add((index, regularQueues[index].Dequeue()));
                tookWindow = true;
            }

            if (!tookWindow)
            {
                break;
            }
        }

        var afterRegularLeftover = regularLimit - regularSelected.Count;
        var selectedHeads = new List<(int CandidateIndex, EvidenceGraphWindow Window)>();
        var headLimit = Math.Min(this._options.MaximumCandidateHeadNodes, afterRegularLeftover);
        foreach (var index in orderedIndices)
        {
            if (selectedHeads.Count >= headLimit)
            {
                break;
            }

            var head = candidateWindowsByIndex[index]
                .Where(window => window.Origins.Contains(EvidenceNodeOrigin.CandidateHead))
                .OrderBy(window => window.Start)
                .FirstOrDefault();
            if (head is not null)
            {
                selectedHeads.Add((index, head));
            }
        }

        var afterHeadLeftover = afterRegularLeftover - selectedHeads.Count;
        changedSelection.Run(changedSelection.SelectedCount + afterHeadLeftover);
        return new EvidenceGraphWindowSelection(
            changedSelection.Selected,
            [.. regularSelected, .. selectedHeads]);
    }

    private static List<EvidenceGraphWindow> Interleave(IReadOnlyList<EvidenceGraphWindow> windows)
    {
        var after = windows
            .Where(window => window.Side == ChangeAnatomySide.After)
            .OrderByDescending(window => window.Salience)
            .ThenBy(window => window.Start)
            .ToArray();
        var before = windows
            .Where(window => window.Side == ChangeAnatomySide.Before)
            .OrderByDescending(window => window.Salience)
            .ThenBy(window => window.Start)
            .ToArray();
        var result = new List<EvidenceGraphWindow>(after.Length + before.Length);
        for (var index = 0; index < Math.Max(after.Length, before.Length); index++)
        {
            if (index < after.Length)
            {
                result.Add(after[index]);
            }

            if (index < before.Length)
            {
                result.Add(before[index]);
            }
        }

        return result;
    }

    private static (
        List<EvidenceNode> Nodes,
        Dictionary<string, List<EvidenceNode>> ChangedByPath,
        Dictionary<int, List<EvidenceNode>> CandidateByIndex)
        BuildNodes(IReadOnlyList<EvidenceNode> factNodes, EvidenceGraphWindowSelection selection, EvidenceGraphBlobCache blobs)
    {
        var nodes = new List<EvidenceNode>(factNodes);
        var changedByPath = new Dictionary<string, List<EvidenceNode>>(StringComparer.Ordinal);
        var candidateByIndex = new Dictionary<int, List<EvidenceNode>>();
        var ordinal = 0;
        foreach (var window in selection.ChangedWindows
            .OrderBy(candidate => candidate.ChangedPath, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Side)
            .ThenBy(candidate => candidate.Start))
        {
            ordinal++;
            var node = CreateWindowNode(window, ordinal, blobs, true);
            nodes.Add(node);
            AppendNode(changedByPath, window.ChangedPath!, node);
        }

        foreach (var (candidateIndex, window) in selection.CandidateWindows
            .OrderBy(entry => entry.CandidateIndex)
            .ThenBy(entry => entry.Window.Start))
        {
            ordinal++;
            var node = CreateWindowNode(window, ordinal, blobs, false);
            nodes.Add(node);
            AppendNode(candidateByIndex, candidateIndex, node);
        }

        return (nodes, changedByPath, candidateByIndex);
    }

    private static EvidenceNode CreateWindowNode(EvidenceGraphWindow window, int ordinal, EvidenceGraphBlobCache blobs, bool isChangedFile)
    {
        var blob = blobs.Get(window.ObjectId);
        var text = string.Join('\n', blob.Lines.Skip(window.Start - 1).Take(window.Length));
        var origins = window.Origins.OrderBy(origin => (int)origin).ToArray();
        return new EvidenceNode(
            WindowNodeId(ordinal),
            window.Path,
            window.Side,
            window.ObjectId,
            window.Start,
            window.End,
            text,
            ContentHash(text),
            isChangedFile,
            origins,
            window.Salience,
            window.Truncated);
    }

    private static void AppendNode<TKey>(IDictionary<TKey, List<EvidenceNode>> nodes, TKey key, EvidenceNode node)
        where TKey : notnull
    {
        if (!nodes.TryGetValue(key, out var list))
        {
            list = [];
            nodes.Add(key, list);
        }

        list.Add(node);
    }

    private static List<EvidenceGraphPendingEdge> BuildPendingEdges(
        ChangeAnatomyModel anatomy,
        CorrespondenceRanking ranking,
        IReadOnlyDictionary<string, List<EvidenceNode>> changedWindowNodesByPath,
        IReadOnlyDictionary<string, EvidenceNode> factByPath,
        IReadOnlyDictionary<int, List<EvidenceNode>> candidateNodesByIndex,
        IReadOnlyDictionary<int, Dictionary<int, double>> contributionsByIndex,
        IDictionary<string, int> skipReasons)
    {
        var pending = new List<EvidenceGraphPendingEdge>();
        for (var index = 0; index < ranking.Candidates.Count; index++)
        {
            var candidate = ranking.Candidates[index];
            var candidateNodes = candidateNodesByIndex.TryGetValue(index, out var existing) ? existing : [];
            var candidatePrimary = candidateNodes.OrderBy(node => node.StartLine).FirstOrDefault();
            foreach (var signal in candidate.Signals)
            {
                switch (signal)
                {
                    case CrossLanguageCorrespondenceSignal:
                        EvidenceGraphBlobCache.Count(skipReasons, "cross-language boost carries no matched value and produced no edge");
                        break;
                    case CoChangeCorrespondenceSignal coChange:
                        AddFileOnlyEdge(
                            pending,
                            skipReasons,
                            PrimaryChangedNode(coChange.ChangedPath, changedWindowNodesByPath, factByPath),
                            candidatePrimary,
                            coChange.Contribution);
                        break;
                    case SharedKeyCorrespondenceSignal shared:
                        AddSharedKeyEdge(
                            pending,
                            anatomy,
                            skipReasons,
                            shared,
                            index,
                            candidateNodes,
                            candidatePrimary,
                            contributionsByIndex,
                            changedWindowNodesByPath,
                            factByPath);
                        break;
                }
            }
        }

        return pending;
    }

    private static void AddFileOnlyEdge(
        List<EvidenceGraphPendingEdge> pending,
        IDictionary<string, int> skipReasons,
        EvidenceNode? from,
        EvidenceNode? to,
        double contribution)
    {
        if (from is null)
        {
            EvidenceGraphBlobCache.Count(skipReasons, "match edge dropped: changed file has no selected node");
            return;
        }

        if (to is null)
        {
            EvidenceGraphBlobCache.Count(skipReasons, "match edge dropped: candidate has no selected node");
            return;
        }

        pending.Add(new EvidenceGraphPendingEdge(
            from.NodeId, to.NodeId, MatchEdgeKind.CoChange, null, MatchEdgeAnchor.FileOnly, MatchEdgeAnchor.FileOnly, null, contribution));
    }

    private static void AddSharedKeyEdge(
        List<EvidenceGraphPendingEdge> pending,
        ChangeAnatomyModel anatomy,
        IDictionary<string, int> skipReasons,
        SharedKeyCorrespondenceSignal shared,
        int candidateIndex,
        IReadOnlyList<EvidenceNode> candidateNodes,
        EvidenceNode? candidatePrimary,
        IReadOnlyDictionary<int, Dictionary<int, double>> contributionsByIndex,
        IReadOnlyDictionary<string, List<EvidenceNode>> changedWindowNodesByPath,
        IReadOnlyDictionary<string, EvidenceNode> factByPath)
    {
        var from = PrimaryChangedNode(shared.ChangedPath, changedWindowNodesByPath, factByPath);
        if (from is null)
        {
            EvidenceGraphBlobCache.Count(skipReasons, "match edge dropped: changed file has no selected node");
            return;
        }

        if (candidatePrimary is null)
        {
            EvidenceGraphBlobCache.Count(skipReasons, "match edge dropped: candidate has no selected node");
            return;
        }

        var to = candidatePrimary;
        var toAnchor = MatchEdgeAnchor.FileOnly;
        if (shared.CandidateLines.Count > 0)
        {
            var contributions = contributionsByIndex.TryGetValue(candidateIndex, out var map) ? map : new Dictionary<int, double>();
            var bestLine = shared.CandidateLines
                .OrderByDescending(line => contributions.GetValueOrDefault(line))
                .ThenBy(line => line)
                .First();
            var covering = candidateNodes
                .Where(node => node.StartLine <= bestLine && bestLine <= node.EndLine)
                .OrderBy(node => node.StartLine)
                .FirstOrDefault();
            if (covering is not null)
            {
                to = covering;
                toAnchor = MatchEdgeAnchor.Quoted;
            }
        }

        var resolvedFrom = from;
        var fromAnchor = MatchEdgeAnchor.PathOnly;
        var anatomyFile = anatomy.Files.FirstOrDefault(file => string.Equals(file.Path, shared.ChangedPath, StringComparison.Ordinal));
        if (anatomyFile is not null)
        {
            var key = anatomyFile.Keys.FirstOrDefault(
                candidateKey => string.Equals(candidateKey.Normalized, shared.MatchedValue, StringComparison.Ordinal));
            if (key is not null)
            {
                var occurrences = key.Occurrences
                    .Where(occurrence => occurrence.Side == ChangeAnatomySide.After)
                    .Concat(key.Occurrences.Where(occurrence => occurrence.Side == ChangeAnatomySide.Before));
                var lineOccurrence = false;
                foreach (var occurrence in occurrences)
                {
                    if (occurrence.LineNumber is not int lineNumber)
                    {
                        continue;
                    }

                    lineOccurrence = true;
                    var quoted = changedWindowNodesByPath.TryGetValue(shared.ChangedPath, out var nodes)
                        ? nodes.FirstOrDefault(node => node.Side == occurrence.Side && node.StartLine <= lineNumber && lineNumber <= node.EndLine)
                        : null;
                    if (quoted is not null)
                    {
                        resolvedFrom = quoted;
                        fromAnchor = MatchEdgeAnchor.Quoted;
                        break;
                    }
                }

                if (fromAnchor != MatchEdgeAnchor.Quoted && lineOccurrence)
                {
                    fromAnchor = MatchEdgeAnchor.FileOnly;
                }
            }
        }

        pending.Add(new EvidenceGraphPendingEdge(
            resolvedFrom.NodeId,
            to.NodeId,
            MapKind(shared.Kind),
            shared.MatchedValue,
            fromAnchor,
            toAnchor,
            shared.DocumentFrequency,
            shared.Contribution));
    }

    private static EvidenceNode? PrimaryChangedNode(
        string changedPath,
        IReadOnlyDictionary<string, List<EvidenceNode>> windowNodes,
        IReadOnlyDictionary<string, EvidenceNode> factNodes)
    {
        if (windowNodes.TryGetValue(changedPath, out var nodes))
        {
            var after = nodes.Where(node => node.Side == ChangeAnatomySide.After).OrderBy(node => node.StartLine).FirstOrDefault();
            if (after is not null)
            {
                return after;
            }

            var before = nodes.Where(node => node.Side == ChangeAnatomySide.Before).OrderBy(node => node.StartLine).FirstOrDefault();
            if (before is not null)
            {
                return before;
            }
        }

        return factNodes.TryGetValue(changedPath, out var fact) ? fact : null;
    }

    private static MatchEdgeKind MapKind(CorrespondenceSignalKind kind) => kind switch
    {
        CorrespondenceSignalKind.SharedLiteral => MatchEdgeKind.SharedLiteral,
        CorrespondenceSignalKind.SharedComment => MatchEdgeKind.SharedComment,
        CorrespondenceSignalKind.PathAffinity => MatchEdgeKind.PathAffinity,
        _ => MatchEdgeKind.SharedIdentifier,
    };

    private static EdgeSelection SelectEdges(
        IReadOnlyList<EvidenceGraphPendingEdge> pendingEdges,
        int maximumEdges,
        int maximumEdgesPerNodePair)
    {
        var deduped = new Dictionary<(string From, string To, MatchEdgeKind Kind, string? Value), EvidenceGraphPendingEdge>();
        foreach (var edge in pendingEdges)
        {
            var key = (edge.FromNodeId, edge.ToNodeId, edge.Kind, edge.MatchedValue);
            if (!deduped.TryGetValue(key, out var existing) || edge.Contribution > existing.Contribution)
            {
                deduped[key] = edge;
            }
        }

        var afterPairCap = new List<EvidenceGraphPendingEdge>();
        var pairDropped = new List<double>();
        foreach (var group in deduped.Values.GroupBy(edge => (edge.FromNodeId, edge.ToNodeId)))
        {
            var ordered = group
                .OrderByDescending(edge => edge.Contribution)
                .ThenBy(edge => edge.Kind)
                .ThenBy(edge => edge.MatchedValue, StringComparer.Ordinal)
                .ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                if (index < maximumEdgesPerNodePair)
                {
                    afterPairCap.Add(ordered[index]);
                }
                else
                {
                    pairDropped.Add(ordered[index].Contribution);
                }
            }
        }

        var orderedEdges = afterPairCap
            .OrderByDescending(edge => edge.Contribution)
            .ThenBy(edge => edge.FromNodeId, StringComparer.Ordinal)
            .ThenBy(edge => edge.ToNodeId, StringComparer.Ordinal)
            .ThenBy(edge => edge.Kind)
            .ThenBy(edge => edge.MatchedValue, StringComparer.Ordinal)
            .ToArray();
        var keptCount = Math.Min(maximumEdges, orderedEdges.Length);
        var floor = keptCount > 0 ? orderedEdges[keptCount - 1].Contribution : 0;
        var maxPairDropped = pairDropped.Count > 0 ? pairDropped.Max() : 0;
        var aboveFloor = pairDropped.Count(contribution => contribution > floor);
        var edges = new List<MatchEdge>(keptCount);
        var ordinal = 0;
        foreach (var edge in orderedEdges.Take(keptCount)
            .OrderBy(candidate => candidate.FromNodeId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.ToNodeId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.MatchedValue, StringComparer.Ordinal))
        {
            ordinal++;
            edges.Add(new MatchEdge(
                EdgeId(ordinal),
                edge.FromNodeId,
                edge.ToNodeId,
                edge.Kind,
                edge.MatchedValue,
                edge.FromAnchor,
                edge.ToAnchor,
                edge.DocumentFrequency,
                edge.Contribution));
        }

        return new EdgeSelection(edges, pairDropped.Count, orderedEdges.Length - keptCount, floor, maxPairDropped, aboveFloor);
    }

    private static int QuotedLineCount(IReadOnlyList<EvidenceNode> nodes) =>
        nodes.Where(node => !node.IsManifestFact).Sum(node => node.EndLine - node.StartLine + 1);

    private static IReadOnlyDictionary<string, int> CountByOrigin(IReadOnlyList<EvidenceNode> nodes)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            foreach (var origin in node.Origins)
            {
                var name = origin.ToString();
                counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
            }
        }

        return counts;
    }

    private static IReadOnlyDictionary<string, int> CountByKind(IReadOnlyList<MatchEdge> edges)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            var name = edge.Kind.ToString();
            counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static EvidenceGraphModel CreateEmptyGraph(
        int changedFileCount,
        int candidateCount,
        IReadOnlyDictionary<string, int> skipReasons)
    {
        var diagnostics = new EvidenceGraphDiagnostics(
            0, 0, 0, 0, 0, changedFileCount, 0, candidateCount, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new SortedDictionary<string, int>(StringComparer.Ordinal),
            new SortedDictionary<string, int>(StringComparer.Ordinal),
            skipReasons);
        return new EvidenceGraphModel([], [], diagnostics);
    }

    private void LogCompletion(EvidenceGraphDiagnostics diagnostics, long started)
    {
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        this._logger.LogInformation(
            "Evidence graph built with {NodeCount} nodes, {ManifestFactNodeCount} manifest facts, and {EdgeCount} edges, " +
            "dropping {DroppedWindowCount} windows and {DroppedEdgeCount} edges in {ElapsedMilliseconds:0.000} ms.",
            diagnostics.NodeCount,
            diagnostics.ManifestFactNodeCount,
            diagnostics.EdgeCount,
            diagnostics.DroppedWindowCount,
            diagnostics.DroppedEdgeCount,
            elapsed);
        this._logger.LogDebug(
            "Evidence graph budgets are {ChangedBudget} changed and {CandidateBudget} candidate quote-window slots; " +
            "the kept-edge floor is {KeptEdgeContributionFloor} with a strongest per-pair drop of {MaxPairDroppedContribution}.",
            diagnostics.ChangedBudget,
            diagnostics.CandidateBudget,
            diagnostics.KeptEdgeContributionFloor,
            diagnostics.MaxPairDroppedContribution);
    }

    private static void ValidateOptions(EvidenceGraphOptions options)
    {
        if (options.MaximumQuoteWindowNodes <= 0
            || options.MaximumQuotedLinesPerNode <= 0
            || options.CandidateWindowLines <= 0
            || options.MaximumNodesPerChangedFile <= 0
            || options.MaximumNodesPerCandidate <= 0
            || options.MaximumCandidateHeadNodes <= 0
            || options.MaximumEdges <= 0
            || options.MaximumEdgesPerNodePair <= 0
            || options.HunkContextLines < 0
            || options.MergeGapLines < 0
            || options.ChangedNodeShare < 0
            || options.ChangedNodeShare > 1)
        {
            throw new InvalidOperationException("Evidence graph options must contain valid bounds and shares.");
        }
    }

    private static string FactNodeId(int ordinal) => "m" + ordinal.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');

    private static string WindowNodeId(int ordinal) => "n" + ordinal.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');

    private static string EdgeId(int ordinal) => "e" + ordinal.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');

    private static string ContentHash(string text) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
