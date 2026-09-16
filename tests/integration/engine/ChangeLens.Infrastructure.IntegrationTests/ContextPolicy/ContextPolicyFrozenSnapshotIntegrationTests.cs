using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.ContextPolicy.Services;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.IntegrationTests.EvidenceGraph.Support;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.ContextPolicy;

/// <summary>
///     Verifies context policy over evidence graphs built from real frozen Git fixtures.
/// </summary>
public sealed class ContextPolicyFrozenSnapshotIntegrationTests
{
    /// <summary>
    ///     Asynchronously redacts a committed secret quoted by a changed hunk so the secret never reaches disclosed text.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task Apply_RedactsCommittedSecretQuotedByChangedHunk()
    {
        const string Secret = "AKIAQWERTYUIOPASDFGH";
        using var repository = new TemporaryGitRepository();
        var lines = new[] { "namespace Billing;", "", "public static class Settings", "{", "    public const string Region = \"eu\";", "}" };
        repository.CommitFile("Settings.cs", string.Join('\n', lines) + "\n", "seed settings");
        var mergeBase = repository.Revision;

        var changed = lines.ToList();
        changed.Insert(5, $"    public const string AccessKey = \"{Secret}\";");
        var head = EvidenceGraphHarness.CommitFiles(repository, "add access key", [("Settings.cs", string.Join('\n', changed) + "\n")]);
        var entry = SnapshotManifestEntryFixtures.CreateEntry(
            repository, mergeBase, head, "Settings.cs", "Settings.cs", SnapshotChangeCategory.Modified);

        var graphResult = await EvidenceGraphHarness.BuildAsync(repository, mergeBase, head, [entry]);

        Assert.True(graphResult.IsSuccess);
        var graph = graphResult.Data!;
        Assert.Contains(graph.Nodes, node => node.QuotedText.Contains(Secret, StringComparison.Ordinal));
        var policy = new ContextPolicyService(new EvidenceGraphOptions(), new ContextPolicyOptions(), NullLogger<ContextPolicyService>.Instance);

        var outcome = policy.Apply(graph, TestContext.Current.CancellationToken);

        var decision = Assert.Single(outcome.Decisions, candidate => candidate.Verdict == ContextPolicyVerdict.Redact);
        var redaction = Assert.Single(decision.Redactions);
        Assert.Equal(6, redaction.LineNumber);
        Assert.Equal("aws access key id", redaction.PatternName);
        Assert.DoesNotContain(outcome.DisclosedNodes, node => node.Text.Contains(Secret, StringComparison.Ordinal));
        Assert.Contains(
            outcome.DisclosedNodes, node => node.IsRedacted && node.Text.Contains("«redacted: aws access key id»", StringComparison.Ordinal));
    }
}
