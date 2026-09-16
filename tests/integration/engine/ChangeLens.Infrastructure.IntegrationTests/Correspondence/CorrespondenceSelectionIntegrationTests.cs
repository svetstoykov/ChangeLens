using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.IntegrationTests.Correspondence.Support;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Correspondence;

/// <summary>
///     Verifies correspondence indexing and candidate selection against real frozen Git fixtures.
/// </summary>
public sealed class CorrespondenceSelectionIntegrationTests
{
    /// <summary>
    ///     Asynchronously distinguishes indexed, skipped, and truncated files while still ranking an unchanged candidate.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_DiagnosticsDistinguishIndexedSkippedAndTruncated()
    {
        using var repository = new TemporaryGitRepository();
        var wideWords = Enumerable.Range(0, 30).Select(index => "wordq" + (char)('a' + index / 26) + (char)('a' + index % 26));
        var wideContent = string.Join(' ', wideWords) + "\n";
        var largeContent = string.Concat(Enumerable.Repeat("quintrelmop ", 200)) + "\n";
        var history = new (string Path, string Content)[]
        {
            ("vendor/lib.txt", "zorblaxquant\n"),
            ("blob.dat", "zor\0blax\n"),
            ("large.txt", largeContent),
            ("wide.txt", wideContent),
            ("consumer.txt", "zorblaxquant\n"),
        };
        CorrespondenceRankingHarness.CommitFiles(repository, "seed indexed fixtures", history);
        var mergeBase = repository.Revision;

        var head = CorrespondenceRankingHarness.CommitFiles(repository, "add subject", [("subject.txt", "zorblaxquant\n")]);
        var entry = SnapshotManifestEntryFixtures.CreateAddedEntry(repository, head, "subject.txt");
        var result = await CorrespondenceRankingHarness.RankAsync(
            repository, mergeBase, head, [entry], new CorrespondenceOptions { MaximumIndexedKeysPerFile = 10 },
            new FrozenGitTreeReaderOptions { MaximumBlobBytes = 256 });

        Assert.True(result.IsSuccess);
        var ranking = Assert.IsType<CorrespondenceRanking>(result.Data);
        var diagnostics = ranking.Diagnostics;
        Assert.Equal(7, diagnostics.TreeFileCount);
        Assert.Equal(6, diagnostics.EligibleFileCount);
        Assert.Equal(4, diagnostics.IndexedFileCount);
        Assert.Equal(3, diagnostics.SkippedFileCount);
        Assert.Equal(1, diagnostics.TruncatedFileCount);
        Assert.Equal(1, diagnostics.SkipReasons["inside 'vendor'"]);
        Assert.Equal(1, diagnostics.SkipReasons["binary content"]);
        Assert.Equal(1, diagnostics.SkipReasons["larger than the configured blob bound"]);
        Assert.Equal(3, diagnostics.SkipReasons.Count);
        Assert.Contains(ranking.Candidates, candidate => candidate.Path == "consumer.txt");
    }

    /// <summary>
    ///     Asynchronously caps returned candidates while counting every matching file and capping display reasons.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_CapsCandidatesAndReasons()
    {
        using var repository = new TemporaryGitRepository();
        var history = new (string Path, string Content)[]
        {
            ("match1.txt", "zorblaxquant quintrelmop\n"),
            ("match2.txt", "zorblaxquant quintrelmop\n"),
            ("match3.txt", "zorblaxquant quintrelmop\n"),
        };
        CorrespondenceRankingHarness.CommitFiles(repository, "seed matching files", history);
        var mergeBase = repository.Revision;

        var head = CorrespondenceRankingHarness.CommitFiles(repository, "add subject", [("subject.txt", "zorblaxquant quintrelmop\n")]);
        var entry = SnapshotManifestEntryFixtures.CreateAddedEntry(repository, head, "subject.txt");
        var result = await CorrespondenceRankingHarness.RankAsync(
            repository, mergeBase, head, [entry],
            new CorrespondenceOptions { MaximumCandidates = 2, MaximumReasonsPerCandidate = 1, IncludeCoChange = false });

        Assert.True(result.IsSuccess);
        var ranking = Assert.IsType<CorrespondenceRanking>(result.Data);
        Assert.Equal(2, ranking.Diagnostics.ReturnedCandidateCount);
        Assert.Equal(3, ranking.Diagnostics.MatchingCandidateCount);
        Assert.Equal(new[] { 1, 2 }, ranking.Candidates.Select(candidate => candidate.Rank).ToArray());
        Assert.All(ranking.Candidates, candidate =>
        {
            Assert.True(candidate.Signals.Count >= 2);
            Assert.Single(candidate.Reasons);
            Assert.Equal(candidate.Signals.Count - 1, candidate.OmittedReasonCount);
            Assert.Equal(candidate.Signals[0], candidate.Reasons[0]);
        });
    }

    /// <summary>
    ///     Asynchronously keeps a second dominant-signal family visible when the share cap would exclude it.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_DominantSignalShareKeepsAnotherFamily()
    {
        using var repository = new TemporaryGitRepository();
        var history = new (string Path, string Content)[]
        {
            ("first.cs", "zorblaxquant();\n"),
            ("second.cs", "zorblaxquant();\n"),
            ("third.cs", "// zorblaxquant\n"),
        };
        CorrespondenceRankingHarness.CommitFiles(repository, "seed dominant signal files", history);
        var mergeBase = repository.Revision;

        var head = CorrespondenceRankingHarness.CommitFiles(repository, "add source", [("source.cs", "zorblaxquant = 1;\n")]);
        var entry = SnapshotManifestEntryFixtures.CreateAddedEntry(repository, head, "source.cs");
        var result = await CorrespondenceRankingHarness.RankAsync(
            repository, mergeBase, head, [entry],
            new CorrespondenceOptions { MaximumCandidates = 2, MaximumDominantSignalShare = 0.5, IncludeCoChange = false });

        Assert.True(result.IsSuccess);
        var ranking = Assert.IsType<CorrespondenceRanking>(result.Data);
        Assert.Equal(3, ranking.Diagnostics.MatchingCandidateCount);
        var returnedPaths = ranking.Candidates.Select(candidate => candidate.Path).ToArray();
        Assert.Contains("third.cs", returnedPaths);
        var commentCandidate = ranking.Candidates.Single(candidate => candidate.Path == "third.cs");
        Assert.Equal(CorrespondenceSignalKind.SharedComment, commentCandidate.DominantSignal);
        Assert.Single(returnedPaths, path => path is "first.cs" or "second.cs");
    }

    /// <summary>
    ///     Asynchronously returns an empty ranking when the change analysis produced no analyzed changed file.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_ReturnsEmptyRankingWithoutAnalyzedChangedFiles()
    {
        using var repository = new TemporaryGitRepository();
        var mergeBase = repository.Revision;

        var head = CorrespondenceRankingHarness.CommitFiles(repository, "add lockfile", [("package-lock.json", "{}\n")]);
        var entry = SnapshotManifestEntryFixtures.CreateAddedEntry(repository, head, "package-lock.json");
        var result = await CorrespondenceRankingHarness.RankAsync(repository, mergeBase, head, [entry]);

        Assert.True(result.IsSuccess);
        var ranking = Assert.IsType<CorrespondenceRanking>(result.Data);
        Assert.Empty(ranking.Candidates);
        Assert.Equal(0, ranking.Diagnostics.AnalyzedChangedFileCount);
        Assert.Equal(0, ranking.Diagnostics.TreeFileCount);
        Assert.Equal(0, ranking.Diagnostics.IndexedFileCount);
        Assert.Empty(ranking.Diagnostics.SkipReasons);
    }
}