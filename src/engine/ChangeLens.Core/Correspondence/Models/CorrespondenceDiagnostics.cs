namespace ChangeLens.Core.Correspondence.Models;

/// <summary>
///     Represents aggregate diagnostics for one correspondence ranking.
/// </summary>
/// <param name="TreeFileCount">The number of files returned by the captured HEAD tree listing.</param>
/// <param name="TreeWasTruncated">Whether the tree listing cap omitted one or more files from indexing.</param>
/// <param name="EligibleFileCount">The number of listed files not excluded by the shared path rules.</param>
/// <param name="IndexedFileCount">The number of files tokenized into the index.</param>
/// <param name="SkippedFileCount">The number of listed files not indexed, for any reason.</param>
/// <param name="TruncatedFileCount">The number of indexed files that hit the per-file indexed key cap.</param>
/// <param name="DistinctIndexedValueCount">The number of distinct normalized keys in the index.</param>
/// <param name="PostingCount">The number of file and key pairs in the index.</param>
/// <param name="CommonQueryValueCount">The number of distinct changed keys dropped as too common to query.</param>
/// <param name="MatchingCandidateCount">The number of unchanged files that scored above zero before selection.</param>
/// <param name="ExcludedChangedCandidateCount">The number of already-changed files removed from the candidate pool.</param>
/// <param name="ReturnedCandidateCount">The number of candidates returned after the candidate cap.</param>
/// <param name="AnalyzedChangedFileCount">The number of analyzed changed files used as queries.</param>
/// <param name="CoveredChangedFileCount">The number of changed files matched by at least one returned candidate.</param>
/// <param name="HistoryCommitsInspected">The number of history commits inspected for co-change, or zero when disabled.</param>
/// <param name="OversizedHistoryCommitsSkipped">The number of history commits ignored for touching too many paths.</param>
/// <param name="HistoryAnchorCommitCount">The number of history commits that changed at least one changed file.</param>
/// <param name="CoChangeSignalCount">The number of co-change signals added to candidates.</param>
/// <param name="SkipReasons">The skipped-file counts by short reason. Cannot be <see langword="null" />.</param>
public sealed record CorrespondenceDiagnostics(
    int TreeFileCount,
    bool TreeWasTruncated,
    int EligibleFileCount,
    int IndexedFileCount,
    int SkippedFileCount,
    int TruncatedFileCount,
    int DistinctIndexedValueCount,
    int PostingCount,
    int CommonQueryValueCount,
    int MatchingCandidateCount,
    int ExcludedChangedCandidateCount,
    int ReturnedCandidateCount,
    int AnalyzedChangedFileCount,
    int CoveredChangedFileCount,
    int HistoryCommitsInspected,
    int OversizedHistoryCommitsSkipped,
    int HistoryAnchorCommitCount,
    int CoChangeSignalCount,
    IReadOnlyDictionary<string, int> SkipReasons);
