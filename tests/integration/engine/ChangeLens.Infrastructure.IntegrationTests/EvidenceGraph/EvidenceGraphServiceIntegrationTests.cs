using System.Security.Cryptography;
using System.Text;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.IntegrationTests.EvidenceGraph.Support;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;
using Xunit;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Infrastructure.IntegrationTests.EvidenceGraph;

/// <summary>
///     Verifies evidence graph construction against real frozen Git fixtures.
/// </summary>
public sealed class EvidenceGraphServiceIntegrationTests
{
    /// <summary>
    ///     Asynchronously quotes a modified file with clamped context on both sides and an exact content hash.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task BuildAsync_ModifiedFileYieldsClampedHunkWindowsAndContentHash()
    {
        using var repository = new TemporaryGitRepository();
        var beforeLines = new[]
        {
            "alpha one", "alpha two", "alpha three", "alpha four",
            "alpha five", "alpha six", "alpha seven", "alpha eight",
        };
        repository.CommitFile("alpha.txt", string.Join('\n', beforeLines) + "\n", "seed alpha");
        var mergeBase = repository.Revision;

        var afterLines = beforeLines.ToArray();
        afterLines[1] = "alpha two changed";
        var head = EvidenceGraphHarness.CommitFiles(repository, "modify alpha", [("alpha.txt", string.Join('\n', afterLines) + "\n")]);
        var entry = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "alpha.txt", "alpha.txt", SnapshotChangeCategory.Modified);

        var result = await EvidenceGraphHarness.BuildAsync(repository, mergeBase, head, [entry]);

        Assert.True(result.IsSuccess);
        var graph = Assert.IsType<EvidenceGraphModel>(result.Data);
        var afterNode = Assert.Single(
            graph.Nodes, node => node.Path == "alpha.txt" && node.Side == ChangeAnatomySide.After && !node.IsManifestFact);
        Assert.Equal(1, afterNode.StartLine);
        Assert.Equal(5, afterNode.EndLine);
        var expectedAfter = string.Join('\n', afterLines.Take(5));
        Assert.Equal(expectedAfter, afterNode.QuotedText);
        Assert.Equal("sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(expectedAfter))), afterNode.ContentHash);
        var beforeNode = Assert.Single(
            graph.Nodes, node => node.Path == "alpha.txt" && node.Side == ChangeAnatomySide.Before && !node.IsManifestFact);
        Assert.Equal(1, beforeNode.StartLine);
        Assert.Equal(5, beforeNode.EndLine);
        Assert.Equal(string.Join('\n', beforeLines.Take(5)), beforeNode.QuotedText);
    }

    /// <summary>
    ///     Asynchronously states a pure rename as a manifest fact that takes no quote-window slot.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task BuildAsync_PureRenameYieldsManifestFactWithoutConsumingWindowBudget()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("old.txt", "renamed payload\n", "seed old");
        repository.CommitFile("other.txt", "other payload\n", "seed other");
        var mergeBase = repository.Revision;

        repository.Move("old.txt", "new.txt");
        repository.WriteFile("other.txt", "other payload\nplus a new line\n");
        repository.Stage("other.txt");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "rename and modify"]);
        var head = repository.Revision;
        var rename = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "old.txt", "new.txt", SnapshotChangeCategory.Renamed);
        var modified = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "other.txt", "other.txt", SnapshotChangeCategory.Modified);
        var options = new EvidenceGraphOptions { MaximumQuoteWindowNodes = 1, ChangedNodeShare = 1.0 };

        var result = await EvidenceGraphHarness.BuildAsync(repository, mergeBase, head, [rename, modified], options);

        Assert.True(result.IsSuccess);
        var graph = Assert.IsType<EvidenceGraphModel>(result.Data);
        var fact = Assert.Single(graph.Nodes, node => node.NodeId == "m001");
        Assert.Equal("manifest fact (Renamed) — path: old.txt → new.txt", fact.QuotedText);
        Assert.Equal(0, fact.StartLine);
        Assert.Equal(0, fact.EndLine);
        Assert.Equal(1, graph.Nodes.Count(node => !node.IsManifestFact));
        Assert.Equal(0, graph.Diagnostics.DroppedWindowCount);
    }

    /// <summary>
    ///     Asynchronously drops windows beyond the budget while keeping the selected count at the budget.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task BuildAsync_DropsWindowsBeyondTheBudget()
    {
        using var repository = new TemporaryGitRepository();
        var lines = Enumerable.Range(1, 30).Select(index => $"budget line {index:00}").ToArray();
        repository.CommitFile("budget.txt", string.Join('\n', lines) + "\n", "seed budget");
        var mergeBase = repository.Revision;

        lines[0] = "budget line 01 changed";
        lines[14] = "budget line 15 changed";
        lines[29] = "budget line 30 changed";
        var head = EvidenceGraphHarness.CommitFiles(repository, "modify budget", [("budget.txt", string.Join('\n', lines) + "\n")]);
        var entry = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "budget.txt", "budget.txt", SnapshotChangeCategory.Modified);
        var options = new EvidenceGraphOptions { MaximumQuoteWindowNodes = 2, ChangedNodeShare = 1.0 };

        var result = await EvidenceGraphHarness.BuildAsync(repository, mergeBase, head, [entry], options);

        Assert.True(result.IsSuccess);
        var graph = Assert.IsType<EvidenceGraphModel>(result.Data);
        Assert.Equal(2, graph.Nodes.Count(node => !node.IsManifestFact));
        Assert.True(graph.Diagnostics.DroppedWindowCount > 0);
    }

    /// <summary>
    ///     Asynchronously anchors a shared-identifier edge on the quote windows of both endpoints.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task BuildAsync_SharedIdentifierProducesQuotedMatchEdge()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("consumer.txt", "zorblaxwidget\n", "seed consumer");
        repository.CommitFile("provider.txt", "initialword\n", "seed provider");
        var mergeBase = repository.Revision;

        var head = EvidenceGraphHarness.CommitFiles(repository, "use shared key", [("provider.txt", "initialword\nzorblaxwidget\n")]);
        var entry = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "provider.txt", "provider.txt", SnapshotChangeCategory.Modified);

        var result = await EvidenceGraphHarness.BuildAsync(repository, mergeBase, head, [entry]);

        Assert.True(result.IsSuccess);
        var graph = Assert.IsType<EvidenceGraphModel>(result.Data);
        var matchNode = Assert.Single(
            graph.Nodes, node => node.Path == "consumer.txt" && node.Origins.Contains(EvidenceNodeOrigin.MatchWindow));
        var edge = Assert.Single(graph.Edges, candidate => candidate.MatchedValue == "zorblaxwidget");
        Assert.Equal(MatchEdgeKind.SharedIdentifier, edge.Kind);
        Assert.Equal(MatchEdgeAnchor.Quoted, edge.FromAnchor);
        Assert.Equal(MatchEdgeAnchor.Quoted, edge.ToAnchor);
        Assert.Equal(matchNode.NodeId, edge.ToNodeId);
    }

    /// <summary>
    ///     Asynchronously counts a binary changed file as skipped content without failing the build.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task BuildAsync_BinaryChangedFileIsCountedAndDoesNotFail()
    {
        using var repository = new TemporaryGitRepository();
        repository.CommitFile("payload.bin", "text before\n", "seed binary");
        var mergeBase = repository.Revision;

        repository.WriteFile("payload.bin", "text before\n\u0000binary after\n");
        repository.Stage("payload.bin");
        TemporaryGitRepository.RunGit(["-C", repository.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "make binary"]);
        var head = repository.Revision;
        var entry = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "payload.bin", "payload.bin", SnapshotChangeCategory.Modified);

        var result = await EvidenceGraphHarness.BuildAsync(repository, mergeBase, head, [entry]);

        Assert.True(result.IsSuccess);
        var graph = Assert.IsType<EvidenceGraphModel>(result.Data);
        Assert.True(graph.Diagnostics.SkipReasons.TryGetValue("binary content", out var count));
        Assert.True(count >= 1);
    }
}
