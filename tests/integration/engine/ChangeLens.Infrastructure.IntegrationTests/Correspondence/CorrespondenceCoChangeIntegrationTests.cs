using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.IntegrationTests.Correspondence.Support;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;

using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Correspondence;

/// <summary>
///     Verifies co-change correspondence ranking against real frozen Git fixtures.
/// </summary>
public sealed class CorrespondenceCoChangeIntegrationTests
{
    /// <summary>
    ///     Asynchronously adds no history signals and reads no history when co-change is disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_CoChangeDisabledProducesNoHistorySignals()
    {
        using var repository = new TemporaryGitRepository();
        CorrespondenceRankingHarness.CommitFiles(repository, "history anchor and partner", [
            ("anchor.txt", "quintrelmop\n"),
            ("partner.txt", "vashtimbrel\n"),
        ]);
        CorrespondenceRankingHarness.CommitFiles(repository, "second anchor and partner change", [
            ("anchor.txt", "quintrelmop felmquist\n"),
            ("partner.txt", "vashtimbrel lerbatsov\n"),
        ]);
        var mergeBase = repository.Revision;
        repository.WriteFile("anchor.txt", "quintrelmop felmquist plinkerdoon\n");
        repository.Stage("anchor.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "change anchor"]);
        var head = repository.Revision;
        var anchorEntry = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "anchor.txt", "anchor.txt", SnapshotChangeCategory.Modified);

        var result = await CorrespondenceRankingHarness.RankAsync(
            repository, mergeBase, head, [anchorEntry], new CorrespondenceOptions { IncludeCoChange = false });

        Assert.True(result.IsSuccess);
        var ranking = result.Data!;
        Assert.DoesNotContain(ranking.Candidates, candidate => candidate.Signals.OfType<CoChangeCorrespondenceSignal>().Any());
        Assert.Equal(0, ranking.Diagnostics.HistoryCommitsInspected);
        Assert.Equal(0, ranking.Diagnostics.HistoryAnchorCommitCount);
        Assert.Equal(0, ranking.Diagnostics.CoChangeSignalCount);
        Assert.Equal(0, ranking.Diagnostics.OversizedHistoryCommitsSkipped);
        Assert.DoesNotContain(ranking.Candidates, candidate => candidate.Path == "partner.txt");
    }

    /// <summary>
    ///     Asynchronously adds a co-change signal to a file that history commits changed together with the changed file.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_CoChangeEnabledAddsHistorySignal()
    {
        using var repository = new TemporaryGitRepository();
        CorrespondenceRankingHarness.CommitFiles(repository, "history anchor and partner", [
            ("anchor.txt", "quintrelmop\n"),
            ("partner.txt", "vashtimbrel\n"),
        ]);
        CorrespondenceRankingHarness.CommitFiles(repository, "second anchor and partner change", [
            ("anchor.txt", "quintrelmop felmquist\n"),
            ("partner.txt", "vashtimbrel lerbatsov\n"),
        ]);
        var mergeBase = repository.Revision;
        repository.WriteFile("anchor.txt", "quintrelmop felmquist plinkerdoon\n");
        repository.Stage("anchor.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "change anchor"]);
        var head = repository.Revision;
        var anchorEntry = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "anchor.txt", "anchor.txt", SnapshotChangeCategory.Modified);

        var result = await CorrespondenceRankingHarness.RankAsync(repository, mergeBase, head, [anchorEntry]);

        Assert.True(result.IsSuccess);
        var ranking = result.Data!;
        var partner = ranking.Candidates.Single(candidate => candidate.Path == "partner.txt");
        var coChange = Assert.Single(partner.Signals.OfType<CoChangeCorrespondenceSignal>());
        Assert.Equal("anchor.txt", coChange.ChangedPath);
        Assert.Equal(2, coChange.CommitCount);
        Assert.True(ranking.Diagnostics.HistoryAnchorCommitCount >= 2);
        Assert.True(ranking.Diagnostics.CoChangeSignalCount >= 1);
        Assert.True(ranking.Diagnostics.HistoryCommitsInspected > 0);
    }

    /// <summary>
    ///     Asynchronously skips oversized history commits and ignores their co-change evidence.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_CoChangeIgnoresOversizedHistoryCommits()
    {
        using var repository = new TemporaryGitRepository();
        CorrespondenceRankingHarness.CommitFiles(repository, "oversized history commit", [
            ("anchor.txt", "quintrelmop\n"),
            ("crowd1.txt", "tavrenok\n"),
            ("crowd2.txt", "bilmandis\n"),
            ("crowd3.txt", "grushkova\n"),
            ("crowd4.txt", "norvelint\n"),
        ]);
        CorrespondenceRankingHarness.CommitFiles(repository, "small history commit", [
            ("anchor.txt", "quintrelmop felmquist\n"),
            ("partner.txt", "vashtimbrel\n"),
        ]);
        var mergeBase = repository.Revision;
        repository.WriteFile("anchor.txt", "quintrelmop felmquist plinkerdoon\n");
        repository.Stage("anchor.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "change anchor"]);
        var head = repository.Revision;
        var anchorEntry = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "anchor.txt", "anchor.txt", SnapshotChangeCategory.Modified);
        var readerOptions = new FrozenGitTreeReaderOptions { MaximumHistoryPathsPerCommit = 3 };

        var result = await CorrespondenceRankingHarness.RankAsync(repository, mergeBase, head, [anchorEntry], readerOptions: readerOptions);

        Assert.True(result.IsSuccess);
        var ranking = result.Data!;
        Assert.True(ranking.Diagnostics.OversizedHistoryCommitsSkipped >= 1);
        var partner = ranking.Candidates.Single(candidate => candidate.Path == "partner.txt");
        Assert.Contains(partner.Signals, signal => signal is CoChangeCorrespondenceSignal);
        Assert.DoesNotContain(ranking.Candidates, candidate => candidate.Path.StartsWith("crowd", StringComparison.Ordinal));
    }
}
