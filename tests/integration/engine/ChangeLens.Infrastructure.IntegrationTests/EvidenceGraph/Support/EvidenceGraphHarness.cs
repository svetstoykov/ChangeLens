using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ChangeAnatomy.Services;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Correspondence.Services;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.EvidenceGraph.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Core.Snapshots.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Correspondence.Support;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Infrastructure.IntegrationTests.EvidenceGraph.Support;

/// <summary>
///     Provides the frozen-snapshot pipeline that evidence graph tests run against.
/// </summary>
internal static class EvidenceGraphHarness
{
    /// <summary>
    ///     Asynchronously builds the real change anatomy and correspondence ranking for a captured snapshot, then builds
    ///     the evidence graph.
    /// </summary>
    /// <param name="repository">The fixture repository. Cannot be <see langword="null" />.</param>
    /// <param name="mergeBase">The captured merge-base revision. Cannot be <see langword="null" />.</param>
    /// <param name="head">The captured head revision. Cannot be <see langword="null" />.</param>
    /// <param name="entries">The captured manifest entries. Cannot be <see langword="null" />.</param>
    /// <param name="options">The evidence graph options, or <see langword="null" /> for defaults.</param>
    /// <param name="correspondenceOptions">The correspondence options, or <see langword="null" /> for defaults.</param>
    /// <param name="readerOptions">The frozen reader bounds, or <see langword="null" /> for defaults.</param>
    /// <param name="logger">The graph logger, or <see langword="null" /> for a null logger.</param>
    /// <returns>A task whose result contains the evidence graph or a frozen-read failure.</returns>
    internal static async Task<Result<EvidenceGraphModel>> BuildAsync(
        TemporaryGitRepository repository,
        string mergeBase,
        string head,
        IReadOnlyList<SnapshotManifestEntry> entries,
        EvidenceGraphOptions? options = null,
        CorrespondenceOptions? correspondenceOptions = null,
        FrozenGitTreeReaderOptions? readerOptions = null,
        ILogger<EvidenceGraphService>? logger = null)
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
        var rankingService = new CorrespondenceRankingService(
            readerFactory,
            anatomyOptions,
            correspondenceOptions ?? new CorrespondenceOptions(),
            NullLogger<CorrespondenceRankingService>.Instance);
        var rankingResult = await rankingService.RankAsync(identity, snapshot, anatomyResult.Data!, cancellationToken);
        Assert.True(rankingResult.IsSuccess);
        var service = new EvidenceGraphService(
            readerFactory, options ?? new EvidenceGraphOptions(), logger ?? NullLogger<EvidenceGraphService>.Instance);
        return await service.BuildAsync(identity, snapshot, anatomyResult.Data!, rankingResult.Data!, cancellationToken);
    }

    /// <summary>
    ///     Writes, stages, and commits several repository-relative files in one commit.
    /// </summary>
    /// <param name="repository">The fixture repository. Cannot be <see langword="null" />.</param>
    /// <param name="message">The commit message. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="files">The paths and exact contents to commit. Cannot be <see langword="null" />.</param>
    /// <returns>The new head revision.</returns>
    internal static string CommitFiles(
        TemporaryGitRepository repository,
        string message,
        IReadOnlyList<(string Path, string Content)> files) =>
        CorrespondenceRankingHarness.CommitFiles(repository, message, files);
}
