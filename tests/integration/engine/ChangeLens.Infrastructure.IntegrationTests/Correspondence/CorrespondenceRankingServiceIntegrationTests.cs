using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Correspondence.Services;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.IntegrationTests.Correspondence.Support;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Correspondence;

/// <summary>
///     Verifies correspondence ranking against real frozen Git fixtures.
/// </summary>
public sealed class CorrespondenceRankingServiceIntegrationTests
{
    /// <summary>
    ///     Asynchronously ranks an unchanged file that shares a rare identifier with the change and excludes every changed file.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_RanksUnchangedFileWithSharedIdentifierAndExcludesChangedFiles()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("consumer.txt", "zorblaxquant\n", "seed consumer");
        repository.CommitFile("bystander.txt", "quintrelmop\n", "seed bystander");
        repository.CommitFile("provider.txt", "plinkerdoon\n", "seed provider");
        repository.CommitFile("sibling.txt", "vashtimbrel\n", "seed sibling");
        var mergeBase = repository.Revision;

        repository.WriteFile("provider.txt", "plinkerdoon\nzorblaxquant\n");
        repository.WriteFile("sibling.txt", "vashtimbrel\nzorblaxquant\n");
        repository.Stage("provider.txt");
        repository.Stage("sibling.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "share key"]);
        var head = repository.Revision;
        var provider = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "provider.txt", "provider.txt", SnapshotChangeCategory.Modified);
        var sibling = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "sibling.txt", "sibling.txt", SnapshotChangeCategory.Modified);

        var result = await CorrespondenceRankingHarness.RankAsync(repository, mergeBase, head, [provider, sibling]);

        Assert.True(result.IsSuccess);
        var ranking = Assert.IsType<CorrespondenceRanking>(result.Data);
        var consumer = Assert.Single(ranking.Candidates, candidate => candidate.Path == "consumer.txt");
        Assert.Contains(consumer.Signals.OfType<SharedKeyCorrespondenceSignal>(), signal => signal.MatchedValue == "zorblaxquant");
        Assert.DoesNotContain(ranking.Candidates, candidate => candidate.Path is "provider.txt" or "sibling.txt");
        Assert.DoesNotContain(ranking.Candidates, candidate => candidate.Path == "bystander.txt");
        Assert.Equal(2, ranking.Diagnostics.ExcludedChangedCandidateCount);
        Assert.Equal(Enumerable.Range(1, ranking.Candidates.Count), ranking.Candidates.Select(candidate => candidate.Rank));
    }

    /// <summary>
    ///     Asynchronously drops a too-common query value while still ranking the rare shared value.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_TooCommonValueIsNotScored()
    {
        using var repository = new TemporaryGitRepository();
        var noiseWords = new[]
        {
            "alphaqz", "betaqz", "gammaqz", "deltaqz", "epsilonqz", "zetazq",
            "thetaqz", "iotazq", "kappazq", "lambdazq", "muqz", "nuqz",
        };
        var history = new List<(string Path, string Content)>();
        for (var index = 0; index < noiseWords.Length; index++)
        {
            history.Add(($"noise{index + 1:00}.txt", $"commonplum {noiseWords[index]}\n"));
        }

        history.Add(("rare.txt", "rarebeacon\n"));
        CorrespondenceRankingHarness.CommitFiles(repository, "seed noise and rare", history);
        var mergeBase = repository.Revision;

        var head = CorrespondenceRankingHarness.CommitFiles(repository, "add subject", [("subject.txt", "commonplum rarebeacon\n")]);
        var subject = SnapshotManifestEntryFixtures.CreateAddedEntry(repository, head, "subject.txt");
        var result = await CorrespondenceRankingHarness.RankAsync(repository, mergeBase, head, [subject]);

        Assert.True(result.IsSuccess);
        var ranking = Assert.IsType<CorrespondenceRanking>(result.Data);
        Assert.True(ranking.Diagnostics.CommonQueryValueCount >= 1);
        Assert.DoesNotContain(
            ranking.Candidates.SelectMany(candidate => candidate.Signals).OfType<SharedKeyCorrespondenceSignal>(),
            signal => signal.MatchedValue == "commonplum");
        var rare = Assert.Single(ranking.Candidates, candidate => candidate.Path == "rare.txt");
        Assert.Equal(1, rare.Rank);
    }

    /// <summary>
    ///     Asynchronously adds a cross-language signal between recognized languages and none for prose.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_AddsCrossLanguageSignalExceptForProse()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("client.ts", "fetch(\"zorblax-refund-route\");\n", "seed client");
        repository.CommitFile("README.md", "Call \"zorblax-refund-route\" to refund.\n", "seed readme");
        var mergeBase = repository.Revision;

        var head = CorrespondenceRankingHarness.CommitFiles(repository, "add api", [("api.py", "ROUTE = \"zorblax-refund-route\"\n")]);
        var api = SnapshotManifestEntryFixtures.CreateAddedEntry(repository, head, "api.py");
        var result = await CorrespondenceRankingHarness.RankAsync(repository, mergeBase, head, [api]);

        Assert.True(result.IsSuccess);
        var ranking = Assert.IsType<CorrespondenceRanking>(result.Data);
        var client = Assert.Single(ranking.Candidates, candidate => candidate.Path == "client.ts");
        var crossLanguage = Assert.Single(client.Signals.OfType<CrossLanguageCorrespondenceSignal>());
        Assert.Equal("python", crossLanguage.ChangedLanguage);
        Assert.Equal("javascript", crossLanguage.CandidateLanguage);
        Assert.Equal(client.Signals.Count - 1, client.Signals.ToList().IndexOf(crossLanguage));
        var readme = ranking.Candidates.SingleOrDefault(candidate => candidate.Path == "README.md");
        if (readme is not null)
        {
            Assert.Empty(readme.Signals.OfType<CrossLanguageCorrespondenceSignal>());
        }
    }

    /// <summary>
    ///     Asynchronously ranks from the captured tree after the worktree copy of a candidate has changed.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_ReadsCapturedTreeNotWorktree()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("consumer.txt", "zorblaxquant\n", "seed consumer");
        repository.CommitFile("provider.txt", "plinkerdoon\n", "seed provider");
        var mergeBase = repository.Revision;

        repository.WriteFile("provider.txt", "plinkerdoon\nzorblaxquant\n");
        repository.Stage("provider.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "share key"]);
        var head = repository.Revision;
        var provider = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "provider.txt", "provider.txt", SnapshotChangeCategory.Modified);
        repository.WriteFile("consumer.txt", "quintrelmop\n");

        var result = await CorrespondenceRankingHarness.RankAsync(repository, mergeBase, head, [provider]);

        Assert.True(result.IsSuccess);
        var ranking = Assert.IsType<CorrespondenceRanking>(result.Data);
        var consumer = Assert.Single(ranking.Candidates, candidate => candidate.Path == "consumer.txt");
        Assert.Contains(consumer.Signals.OfType<SharedKeyCorrespondenceSignal>(), signal => signal.MatchedValue == "zorblaxquant");
    }

    /// <summary>
    ///     Asynchronously omits repository paths and matched key values from information entries.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RankAsync_InformationLogsOmitPathsAndKeyValues()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("consumer.txt", "zorblaxquant\n", "seed consumer");
        repository.CommitFile("bystander.txt", "quintrelmop\n", "seed bystander");
        repository.CommitFile("provider.txt", "plinkerdoon\n", "seed provider");
        repository.CommitFile("sibling.txt", "vashtimbrel\n", "seed sibling");
        var mergeBase = repository.Revision;

        repository.WriteFile("provider.txt", "plinkerdoon\nzorblaxquant\n");
        repository.WriteFile("sibling.txt", "vashtimbrel\nzorblaxquant\n");
        repository.Stage("provider.txt");
        repository.Stage("sibling.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "share key"]);
        var head = repository.Revision;
        var provider = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "provider.txt", "provider.txt", SnapshotChangeCategory.Modified);
        var sibling = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "sibling.txt", "sibling.txt", SnapshotChangeCategory.Modified);
        var logger = new RecordingSnapshotLogger<CorrespondenceRankingService>();

        var result = await CorrespondenceRankingHarness.RankAsync(repository, mergeBase, head, [provider, sibling], logger: logger);

        Assert.True(result.IsSuccess);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information);
        Assert.All(
            logger.Entries.Where(entry => entry.Level >= LogLevel.Information),
            entry =>
            {
                Assert.DoesNotContain("consumer.txt", entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("zorblaxquant", entry.Message, StringComparison.Ordinal);
            });
    }
}
