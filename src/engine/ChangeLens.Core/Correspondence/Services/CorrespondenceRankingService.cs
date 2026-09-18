using System.Diagnostics;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ChangeAnatomy.Helpers;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.Correspondence.Helpers;
using ChangeLens.Core.Correspondence.Interfaces;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;
using Microsoft.Extensions.Logging;
using ChangeAnatomyModel = ChangeLens.Core.ChangeAnatomy.Models.ChangeAnatomy;

namespace ChangeLens.Core.Correspondence.Services;

/// <summary>
///     Ranks unchanged captured files that share rare text or first-parent history with a change.
/// </summary>
/// <remarks>
///     <para>
///         The Engine registers this service as scoped. It serves one analysis request and does not need to be
///         thread-safe.
///     </para>
///     <para>
///         The service indexes only the captured HEAD tree through a snapshot-scoped reader. It never reads the
///         worktree.
///     </para>
///     <para>
///         Already-changed paths are indexed so their keys still count toward key frequency, but they are never
///         returned as candidates.
///     </para>
///     <para>
///         A candidate score directs attention. It is not a proven dependency.
///     </para>
/// </remarks>
/// <param name="readerFactory">The factory that opens the frozen-tree reader for the snapshot. Cannot be <see langword="null" />.</param>
/// <param name="anatomyOptions">The anatomy bounds shared by tokenization. Cannot be <see langword="null" />.</param>
/// <param name="options">The correspondence bounds and weights. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for ranking flow and outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="readerFactory" />, <paramref name="anatomyOptions" />, <paramref name="options" />, or
///     <paramref name="logger" /> is <see langword="null" />.
/// </exception>
public sealed class CorrespondenceRankingService(
    IFrozenGitTreeReaderFactory readerFactory,
    ChangeAnatomyOptions anatomyOptions,
    CorrespondenceOptions options,
    ILogger<CorrespondenceRankingService> logger) : ICorrespondenceRankingService
{
    private readonly IFrozenGitTreeReaderFactory _readerFactory = readerFactory ?? throw new ArgumentNullException(nameof(readerFactory));
    private readonly ChangeAnatomyOptions _anatomyOptions = anatomyOptions ?? throw new ArgumentNullException(nameof(anatomyOptions));
    private readonly CorrespondenceOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<CorrespondenceRankingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<Result<CorrespondenceRanking>> RankAsync(
        AnalysisRepositoryIdentity repository,
        SnapshotManifest snapshot,
        ChangeAnatomyModel anatomy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(anatomy);
        ValidateOptions(this._options, this._anatomyOptions);
        cancellationToken.ThrowIfCancellationRequested();
        var started = Stopwatch.GetTimestamp();
        var changedFiles = anatomy.Files.Where(file => file.IsAnalyzed).ToArray();
        if (changedFiles.Length == 0)
        {
            this._logger.LogInformation("Correspondence ranking skipped because the change anatomy contains no analyzed files.");
            return CreateEmptyRanking();
        }

        var readerResult = this._readerFactory.Open(repository, snapshot);
        if (readerResult.IsFailure)
        {
            return Result.ErrorFromResult<CorrespondenceRanking>(readerResult);
        }

        var reader = readerResult.Data!;
        var listingResult = await reader.ListTreeAsync(cancellationToken);
        if (listingResult.IsFailure)
        {
            return Result.ErrorFromResult<CorrespondenceRanking>(listingResult);
        }

        var listing = listingResult.Data!;
        var skipReasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var indexedFiles = new List<CorrespondenceIndexedFile>(listing.Files.Count);
        var truncatedFileCount = 0;
        var eligibleFileCount = 0;
        var tokenizer = new ChangeAnatomyTokenizer(this._anatomyOptions.MinimumKeyLength);
        foreach (var file in listing.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exclusion = ChangeAnatomyPathRules.ExclusionReason(file.Path);
            if (exclusion is not null)
            {
                Count(skipReasons, exclusion);
                continue;
            }

            eligibleFileCount++;
            var blobResult = await reader.ReadBlobAsync(file.ObjectId, cancellationToken);
            if (blobResult.IsFailure)
            {
                return Result.ErrorFromResult<CorrespondenceRanking>(blobResult);
            }

            var blob = blobResult.Data!;
            if (!blob.HasText)
            {
                Count(skipReasons, blob.SkipReason is FrozenGitBlobSkipReason.Binary ? "binary content" : "larger than the configured blob bound");
                continue;
            }

            var collector = new CorrespondenceIndexedKeyCollector(
                this._options.MaximumIndexedKeysPerFile, this._anatomyOptions.MaximumOccurrencesPerKey);
            Action<string, ChangeAnatomyKeyKind, int?, string> emit = collector.Add;
            tokenizer.TokenizePath(file.Path, emit);
            var state = ChangeAnatomyLexicalState.ForPath(file.Path);
            for (var lineIndex = 0; lineIndex < blob.Lines.Count; lineIndex++)
            {
                tokenizer.TokenizeLine(blob.Lines[lineIndex], lineIndex + 1, ref state, emit, cancellationToken);
            }

            if (collector.Truncated)
            {
                truncatedFileCount++;
            }

            var language = CorrespondenceLanguageRules.LanguageFor(file.Path);
            indexedFiles.Add(new CorrespondenceIndexedFile(indexedFiles.Count, file.Path, file.ObjectId, language, collector.Build()));
        }

        var postings = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var file in indexedFiles)
        {
            foreach (var token in file.Tokens.Values)
            {
                if (!postings.TryGetValue(token.Normalized, out var postingList))
                {
                    postingList = [];
                    postings.Add(token.Normalized, postingList);
                }

                postingList.Add(file.Id);
            }
        }

        var commonQueryValues = new HashSet<string>(StringComparer.Ordinal);
        var accumulators = new Dictionary<int, CorrespondenceCandidateAccumulator>();
        var commonFileCount = (int)Math.Ceiling(indexedFiles.Count * this._options.CommonKeyFileRatio);
        var commonThreshold = Math.Max(this._options.CommonKeyMinimumFileCount, commonFileCount);
        this.ScoreSharedKeys(changedFiles, indexedFiles, postings, commonThreshold, commonQueryValues, accumulators, cancellationToken);
        var historyCommitsInspected = 0;
        var oversizedHistoryCommitsSkipped = 0;
        var historyAnchorCommitCount = 0;
        var coChangeSignalCount = 0;
        if (this._options.IncludeCoChange)
        {
            var historyResult = await reader.ReadHistoryAsync(cancellationToken);
            if (historyResult.IsFailure)
            {
                return Result.ErrorFromResult<CorrespondenceRanking>(historyResult);
            }

            var history = historyResult.Data!;
            historyCommitsInspected = history.CommitsInspected;
            oversizedHistoryCommitsSkipped = history.OversizedCommitsSkipped;
            (historyAnchorCommitCount, coChangeSignalCount) = this.AddCoChangeSignals(history, changedFiles, indexedFiles, accumulators);
        }

        var changedPathSet = snapshot.Entries.Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal);
        var excludedChangedCandidateCount = 0;
        var candidates = new List<CorrespondenceCandidate>(accumulators.Count);
        foreach (var accumulator in accumulators.Values)
        {
            var candidate = accumulator.Build(this._options.MaximumReasonsPerCandidate);
            if (changedPathSet.Contains(candidate.Path))
            {
                excludedChangedCandidateCount++;
                continue;
            }

            if (candidate.Score <= 0)
            {
                continue;
            }

            candidates.Add(candidate);
        }

        var ordered = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
            .ToArray();
        var selected = this.SelectDiverse(ordered, this._options.MaximumCandidates);
        var ranked = selected.Select((candidate, index) => candidate with { Rank = index + 1 }).ToArray();
        var coveredChangedFileCount = ranked.SelectMany(candidate => candidate.MatchedChangedPaths).Distinct(StringComparer.Ordinal).Count();
        var diagnostics = new CorrespondenceDiagnostics(
            listing.Files.Count, listing.WasTruncated, eligibleFileCount, indexedFiles.Count, listing.Files.Count - indexedFiles.Count,
            truncatedFileCount, postings.Count, postings.Values.Sum(postingList => postingList.Count), commonQueryValues.Count, ordered.Length,
            excludedChangedCandidateCount, ranked.Length, changedFiles.Length, coveredChangedFileCount, historyCommitsInspected,
            oversizedHistoryCommitsSkipped, historyAnchorCommitCount, coChangeSignalCount, skipReasons);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        this._logger.LogInformation(
            "Correspondence ranking completed with {IndexedFileCount} of {TreeFileCount} tree files indexed, " +
            "{CommonQueryValueCount} common query values, {ReturnedCandidateCount} of {MatchingCandidateCount} candidates " +
            "returned, and {CoChangeSignalCount} co-change signals in {ElapsedMilliseconds:0.000} ms.",
            indexedFiles.Count, listing.Files.Count, commonQueryValues.Count, ranked.Length, ordered.Length, coChangeSignalCount, elapsed);
        if (listing.WasTruncated)
        {
            this._logger.LogWarning("Correspondence ranking indexed a truncated tree listing; some captured tree files were not considered.");
        }

        if (truncatedFileCount > 0)
        {
            this._logger.LogWarning(
                "Correspondence ranking truncated {TruncatedFileCount} file(s) at the configured {MaximumIndexedKeysPerFile}-key cap.",
                truncatedFileCount, this._options.MaximumIndexedKeysPerFile);
        }

        this._logger.LogDebug(
            "Correspondence common query threshold is {CommonThreshold} of {IndexedFileCount} indexed files.", commonThreshold, indexedFiles.Count);
        if (this._options.IncludeCoChange)
        {
            this._logger.LogDebug(
                "Correspondence co-change inspected {HistoryCommitsInspected} commit(s), skipped {OversizedHistoryCommitsSkipped} oversized, " +
                "matched {HistoryAnchorCommitCount} anchor commit(s), and added {CoChangeSignalCount} signal(s).",
                historyCommitsInspected, oversizedHistoryCommitsSkipped, historyAnchorCommitCount, coChangeSignalCount);
        }

        if (ordered.Length > this._options.MaximumCandidates)
        {
            var selectedPaths = ranked.Select(candidate => candidate.Path).ToHashSet(StringComparer.Ordinal);
            var displacedCount = ordered.Take(this._options.MaximumCandidates).Count(candidate => !selectedPaths.Contains(candidate.Path));
            this._logger.LogDebug(
                "Correspondence diversity selection displaced {DisplacedCount} candidate(s) from the naive top {MaximumCandidates}.",
                displacedCount, this._options.MaximumCandidates);
        }

        return new CorrespondenceRanking(ranked, diagnostics);
    }

    /// <summary>
    ///     Adds recency-weighted co-change signals from first-parent history.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The reader walks history from the merge base, so the commits under analysis never co-change with
    ///         themselves.
    ///     </para>
    ///     <para>
    ///         History names a renamed file by its old path, so anchors match either spelling and are reported under the
    ///         current path. One commit that names both spellings still counts once. Each pair is capped, and a
    ///         candidate's pairs are scaled together rather than truncated, so every changed file it co-changed with
    ///         stays visible while history cannot outweigh rare shared text.
    ///     </para>
    /// </remarks>
    /// <param name="history">The bounded history scan.</param>
    /// <param name="changedFiles">The analyzed changed files.</param>
    /// <param name="indexedFiles">The indexed tree files by id.</param>
    /// <param name="accumulators">The candidate accumulators by indexed file id.</param>
    /// <returns>
    ///     The number of commits that changed a changed file, and the number of signals added. Together they tell a history
    ///     window that never saw a changed file from one whose signals were all zero.
    /// </returns>
    private (int AnchorCommitCount, int SignalCount) AddCoChangeSignals(
        FrozenGitHistoryScan history,
        IReadOnlyList<ChangedFileAnatomy> changedFiles,
        IReadOnlyList<CorrespondenceIndexedFile> indexedFiles,
        IDictionary<int, CorrespondenceCandidateAccumulator> accumulators)
    {
        var currentPathByHistoricalPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in changedFiles)
        {
            currentPathByHistoricalPath[file.Path] = file.Path;
            if (file.OriginalPath is not null)
            {
                currentPathByHistoricalPath[file.OriginalPath] = file.Path;
            }
        }

        var indexedByPath = indexedFiles.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var coChanges = new Dictionary<(string ChangedPath, int CandidateId), (int Count, double WeightedCount)>();
        var anchorCommitCount = 0;
        foreach (var commit in history.Commits)
        {
            var commitPaths = commit.Paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var anchors = commitPaths.Where(currentPathByHistoricalPath.ContainsKey)
                .Select(path => currentPathByHistoricalPath[path]).Distinct(StringComparer.Ordinal).ToArray();
            if (anchors.Length == 0)
            {
                continue;
            }

            anchorCommitCount++;
            var recency = Math.Pow(0.5, commit.Ordinal / this._options.CoChangeHalfLifeInCommits);
            foreach (var anchor in anchors)
            {
                foreach (var path in commitPaths)
                {
                    if (path.Equals(anchor, StringComparison.Ordinal) || !indexedByPath.TryGetValue(path, out var candidateFile))
                    {
                        continue;
                    }

                    var key = (anchor, candidateFile.Id);
                    var tally = coChanges.GetValueOrDefault(key);
                    coChanges[key] = (tally.Count + 1, tally.WeightedCount + recency);
                }
            }
        }

        var signalCount = 0;
        var pairMaximum = this._options.MaximumCoChangeContributionPerPair;
        foreach (var group in coChanges.GroupBy(entry => entry.Key.CandidateId).OrderBy(group => group.Key))
        {
            var pairs = group
                .Select(entry => (
                    entry.Key.ChangedPath,
                    entry.Value.Count,
                    Contribution: Math.Min(pairMaximum, entry.Value.WeightedCount * this._options.CoChangeWeight)))
                .Where(pair => pair.Contribution > 0)
                .OrderByDescending(pair => pair.Contribution)
                .ThenBy(pair => pair.ChangedPath, StringComparer.Ordinal)
                .ToArray();
            if (pairs.Length == 0)
            {
                continue;
            }

            var total = pairs.Sum(pair => pair.Contribution);
            var maximum = this._options.MaximumCoChangeContributionPerCandidate;
            var scale = total > maximum ? maximum / total : 1.0;
            if (!accumulators.TryGetValue(group.Key, out var accumulator))
            {
                accumulator = new CorrespondenceCandidateAccumulator(indexedFiles[group.Key]);
                accumulators.Add(group.Key, accumulator);
            }

            foreach (var pair in pairs)
            {
                accumulator.Add(new CoChangeCorrespondenceSignal(pair.ChangedPath, pair.Contribution * scale, pair.Count));
                signalCount++;
            }
        }

        return (anchorCommitCount, signalCount);
    }

    /// <summary>
    ///     Selects at most <paramref name="limit" /> candidates while keeping one signal family from filling the list.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The first pass admits candidates in score order until each dominant family reaches its share of the limit.
    ///     </para>
    ///     <para>
    ///         Remaining slots go first to deferred candidates that match a changed file no selected candidate covers yet,
    ///         so a small changed file's counterpart is not crowded out by heavily matched changed files. Any slots left are
    ///         then filled in score order.
    ///     </para>
    /// </remarks>
    /// <param name="ordered">The scored candidates ordered by descending score and then path.</param>
    /// <param name="limit">The maximum number of candidates to select.</param>
    /// <returns>The selected candidates ordered by descending score and then path.</returns>
    private IReadOnlyList<CorrespondenceCandidate> SelectDiverse(IReadOnlyList<CorrespondenceCandidate> ordered, int limit)
    {
        if (ordered.Count <= limit)
        {
            return ordered;
        }

        var maximumPerFamily = Math.Max(1, (int)Math.Ceiling(limit * this._options.MaximumDominantSignalShare));
        var familyCounts = new Dictionary<CorrespondenceSignalKind, int>();
        var selected = new List<CorrespondenceCandidate>(limit);
        var deferred = new List<CorrespondenceCandidate>();
        foreach (var candidate in ordered)
        {
            var count = familyCounts.GetValueOrDefault(candidate.DominantSignal);
            if (selected.Count < limit && count < maximumPerFamily)
            {
                selected.Add(candidate);
                familyCounts[candidate.DominantSignal] = count + 1;
            }
            else
            {
                deferred.Add(candidate);
            }
        }

        var taken = new bool[deferred.Count];
        if (selected.Count < limit)
        {
            var covered = selected.SelectMany(candidate => candidate.MatchedChangedPaths).ToHashSet(StringComparer.Ordinal);
            for (var index = 0; index < deferred.Count && selected.Count < limit; index++)
            {
                if (deferred[index].MatchedChangedPaths.All(covered.Contains))
                {
                    continue;
                }

                selected.Add(deferred[index]);
                covered.UnionWith(deferred[index].MatchedChangedPaths);
                taken[index] = true;
            }
        }

        for (var index = 0; index < deferred.Count && selected.Count < limit; index++)
        {
            if (!taken[index])
            {
                selected.Add(deferred[index]);
            }
        }

        return selected.OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.Path, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    ///     Adds shared-key and cross-language signals for every analyzed changed file.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A match is only as strong as its weaker end: a literal in the changed file that matches a bare identifier in
    ///         the candidate counts as identifier evidence, so the signal never claims more than both files support.
    ///     </para>
    ///     <para>
    ///         The cross-language boost belongs to a file pair rather than to each shared key, so it is summed per
    ///         candidate and published as one signal per changed file.
    ///     </para>
    /// </remarks>
    /// <param name="changedFiles">The analyzed changed files used as queries.</param>
    /// <param name="indexedFiles">The indexed tree files by id.</param>
    /// <param name="postings">The indexed file ids by normalized key.</param>
    /// <param name="commonThreshold">The largest document frequency a key may have and still be queried.</param>
    /// <param name="commonQueryValues">The distinct keys dropped as too common, updated by this method.</param>
    /// <param name="accumulators">The candidate accumulators by indexed file id, updated by this method.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe between changed files.</param>
    private void ScoreSharedKeys(
        IReadOnlyList<ChangedFileAnatomy> changedFiles,
        IReadOnlyList<CorrespondenceIndexedFile> indexedFiles,
        IReadOnlyDictionary<string, List<int>> postings,
        int commonThreshold,
        ISet<string> commonQueryValues,
        IDictionary<int, CorrespondenceCandidateAccumulator> accumulators,
        CancellationToken cancellationToken)
    {
        foreach (var changedFile in changedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var changedLanguage = CorrespondenceLanguageRules.LanguageFor(changedFile.Path);
            var crossLanguageBonus = new Dictionary<int, double>();
            foreach (var group in changedFile.Keys.GroupBy(key => key.Normalized, StringComparer.Ordinal))
            {
                if (!postings.TryGetValue(group.Key, out var matches))
                {
                    continue;
                }

                var documentFrequency = matches.Count;
                if (documentFrequency > commonThreshold)
                {
                    commonQueryValues.Add(group.Key);
                    continue;
                }

                var idf = Math.Log((indexedFiles.Count + 1.0) / (documentFrequency + 1.0)) + 1.0;
                var changedKind = this.Strongest(group.Select(key => key.Kind).ToHashSet());
                foreach (var candidateId in matches)
                {
                    var candidateFile = indexedFiles[candidateId];
                    if (candidateFile.Path.Equals(changedFile.Path, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var candidateToken = candidateFile.Tokens[group.Key];
                    var candidateKind = this.Strongest(candidateToken.Kinds);
                    var pairKind = this.Weight(changedKind) <= this.Weight(candidateKind) ? changedKind : candidateKind;
                    var contribution = idf * this.Weight(pairKind);
                    if (contribution <= 0)
                    {
                        continue;
                    }

                    if (!accumulators.TryGetValue(candidateId, out var accumulator))
                    {
                        accumulator = new CorrespondenceCandidateAccumulator(candidateFile);
                        accumulators.Add(candidateId, accumulator);
                    }

                    var family = Family(pairKind);
                    accumulator.Add(new SharedKeyCorrespondenceSignal(
                        family, changedFile.Path, contribution, group.Key, changedKind, candidateKind, documentFrequency, candidateToken.Lines));
                    if (CorrespondenceLanguageRules.CrossesLanguage(changedLanguage, candidateFile.Language))
                    {
                        var boost = contribution * this._options.CrossLanguageBoost;
                        crossLanguageBonus[candidateId] = crossLanguageBonus.GetValueOrDefault(candidateId) + boost;
                    }
                }
            }

            foreach (var entry in crossLanguageBonus.OrderBy(pair => pair.Key))
            {
                if (entry.Value <= 0)
                {
                    continue;
                }

                var candidateLanguage = indexedFiles[entry.Key].Language!;
                var signal = new CrossLanguageCorrespondenceSignal(changedFile.Path, entry.Value, changedLanguage!, candidateLanguage);
                accumulators[entry.Key].Add(signal);
            }
        }
    }

    /// <summary>
    ///     Returns the key kind with the highest configured weight, preferring the earlier declared kind on ties.
    /// </summary>
    /// <param name="kinds">The kinds one file gave a key. Cannot be <see langword="null" />.</param>
    /// <returns>The strongest kind, or <see cref="ChangeAnatomyKeyKind.Identifier" /> when the set is empty.</returns>
    private ChangeAnatomyKeyKind Strongest(IReadOnlySet<ChangeAnatomyKeyKind> kinds)
    {
        var strongest = ChangeAnatomyKeyKind.Identifier;
        var strongestWeight = double.NegativeInfinity;
        foreach (var kind in Enum.GetValues<ChangeAnatomyKeyKind>())
        {
            if (!kinds.Contains(kind))
            {
                continue;
            }

            var weight = this.Weight(kind);
            if (weight > strongestWeight)
            {
                strongest = kind;
                strongestWeight = weight;
            }
        }

        return strongest;
    }

    private double Weight(ChangeAnatomyKeyKind kind) => kind switch
    {
        ChangeAnatomyKeyKind.Identifier => this._options.IdentifierWeight,
        ChangeAnatomyKeyKind.IdentifierPart => this._options.IdentifierPartWeight,
        ChangeAnatomyKeyKind.StringLiteral => this._options.StringLiteralWeight,
        ChangeAnatomyKeyKind.LiteralSegment => this._options.LiteralSegmentWeight,
        ChangeAnatomyKeyKind.PathStem => this._options.PathStemWeight,
        ChangeAnatomyKeyKind.CommentWord => this._options.CommentWordWeight,
        _ => 0.0,
    };

    private static void Count(IDictionary<string, int> counts, string reason) =>
        counts[reason] = counts.TryGetValue(reason, out var count) ? count + 1 : 1;

    private static CorrespondenceRanking CreateEmptyRanking()
    {
        var diagnostics = new CorrespondenceDiagnostics(
            0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new SortedDictionary<string, int>(StringComparer.Ordinal));
        return new CorrespondenceRanking([], diagnostics);
    }

    private static CorrespondenceSignalKind Family(ChangeAnatomyKeyKind kind) => kind switch
    {
        ChangeAnatomyKeyKind.StringLiteral or ChangeAnatomyKeyKind.LiteralSegment => CorrespondenceSignalKind.SharedLiteral,
        ChangeAnatomyKeyKind.PathStem => CorrespondenceSignalKind.PathAffinity,
        ChangeAnatomyKeyKind.CommentWord => CorrespondenceSignalKind.SharedComment,
        _ => CorrespondenceSignalKind.SharedIdentifier,
    };

    private static void ValidateOptions(CorrespondenceOptions options, ChangeAnatomyOptions anatomyOptions)
    {
        if (options.MaximumCandidates <= 0
            || options.MaximumIndexedKeysPerFile <= 0
            || options.MaximumReasonsPerCandidate <= 0
            || options.CommonKeyMinimumFileCount <= 0
            || options.IdentifierWeight < 0
            || options.IdentifierPartWeight < 0
            || options.StringLiteralWeight < 0
            || options.LiteralSegmentWeight < 0
            || options.PathStemWeight < 0
            || options.CommentWordWeight < 0
            || options.CrossLanguageBoost < 0
            || options.CoChangeWeight < 0
            || options.MaximumCoChangeContributionPerPair <= 0
            || options.MaximumCoChangeContributionPerCandidate <= 0
            || options.CoChangeHalfLifeInCommits <= 0
            || options.CommonKeyFileRatio <= 0
            || options.CommonKeyFileRatio > 1
            || options.MaximumDominantSignalShare <= 0
            || options.MaximumDominantSignalShare > 1
            || anatomyOptions.MinimumKeyLength <= 0
            || anatomyOptions.MaximumOccurrencesPerKey <= 0)
        {
            throw new InvalidOperationException("Correspondence options must contain valid bounds and weights.");
        }
    }
}
