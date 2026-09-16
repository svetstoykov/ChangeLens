using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ChangeAnatomy.Services;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Correspondence.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Core.Snapshots.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Correspondence.Support;

/// <summary>
///     Provides the frozen-snapshot pipeline that correspondence ranking tests run against.
/// </summary>
internal static class CorrespondenceRankingHarness
{
    /// <summary>
    ///     Writes, stages, and commits several repository-relative files in one commit.
    /// </summary>
    /// <param name="repository">The fixture repository. Cannot be <see langword="null" />.</param>
    /// <param name="message">The commit message. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="files">The paths and exact contents to commit. Cannot be <see langword="null" />.</param>
    /// <returns>The new head revision.</returns>
    internal static string CommitFiles(TemporaryGitRepository repository, string message, IReadOnlyList<(string Path, string Content)> files)
    {
        foreach (var (path, content) in files)
        {
            repository.WriteFile(path, content);
            repository.Stage(path);
        }

        var output = TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", message]);
        Assert.Equal(0, output.ExitCode);
        return repository.Revision;
    }

    /// <summary>
    ///     Asynchronously extracts the real change anatomy for a captured snapshot and ranks correspondence against it.
    /// </summary>
    /// <param name="repository">The fixture repository. Cannot be <see langword="null" />.</param>
    /// <param name="mergeBase">The captured merge-base revision. Cannot be <see langword="null" />.</param>
    /// <param name="head">The captured head revision. Cannot be <see langword="null" />.</param>
    /// <param name="entries">The captured manifest entries. Cannot be <see langword="null" />.</param>
    /// <param name="options">The correspondence options, or <see langword="null" /> for defaults.</param>
    /// <param name="readerOptions">The frozen reader bounds, or <see langword="null" /> for defaults.</param>
    /// <param name="logger">The ranking logger, or <see langword="null" /> for a null logger.</param>
    /// <returns>A task whose result contains the correspondence ranking or a frozen-read failure.</returns>
    internal static async Task<Result<CorrespondenceRanking>> RankAsync(
        TemporaryGitRepository repository,
        string mergeBase,
        string head,
        IReadOnlyList<SnapshotManifestEntry> entries,
        CorrespondenceOptions? options = null,
        FrozenGitTreeReaderOptions? readerOptions = null,
        ILogger<CorrespondenceRankingService>? logger = null)
    {
        var key = Path.GetFullPath(repository.RootPath);
        var snapshot = new SnapshotManifest(Guid.NewGuid(), new string('a', 64), key, "fixture", head, head, mergeBase, entries);
        var identity = new AnalysisRepositoryIdentity(Guid.NewGuid(), "fixture", key, key, head);
        var readerFactory = new FrozenGitTreeReaderFactory(
            new GitCliCommandRunner(), readerOptions ?? new FrozenGitTreeReaderOptions(), NullLoggerFactory.Instance);
        var anatomyOptions = new ChangeAnatomyOptions();
        var cancellationToken = TestContext.Current.CancellationToken;
        var anatomyService = new ChangeAnatomyService(readerFactory, anatomyOptions, NullLogger<ChangeAnatomyService>.Instance);
        var anatomyResult = await anatomyService.AnalyzeAsync(identity, snapshot, cancellationToken);
        Assert.True(anatomyResult.IsSuccess);

        var service = new CorrespondenceRankingService(
            readerFactory, anatomyOptions, options ?? new CorrespondenceOptions(), logger ?? NullLogger<CorrespondenceRankingService>.Instance);
        return await service.RankAsync(identity, snapshot, anatomyResult.Data!, cancellationToken);
    }
}
