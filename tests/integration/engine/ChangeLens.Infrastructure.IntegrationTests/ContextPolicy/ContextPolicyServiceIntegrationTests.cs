using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.ContextPolicy.Services;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Infrastructure.IntegrationTests.ContextPolicy.Support;
using ChangeLens.Infrastructure.IntegrationTests.Snapshots.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.ContextPolicy;

/// <summary>
///     Verifies context policy disclosure, redaction, exclusion, and edge filtering over in-memory graphs.
/// </summary>
public sealed class ContextPolicyServiceIntegrationTests
{
    /// <summary>
    ///     Discloses a clean node unchanged with a null disclosed hash.
    /// </summary>
    [Fact]
    public void Apply_AllowsCleanNodeUnchanged()
    {
        const string Text = "alpha\nbeta\ngamma";
        var node = ContextPolicyGraphFixtures.CreateNode("n001", "src/app.cs", Text);
        var service = CreateService();

        var outcome = service.Apply(ContextPolicyGraphFixtures.CreateGraph([node]), TestContext.Current.CancellationToken);

        var decision = Assert.Single(outcome.Decisions);
        Assert.Equal(ContextPolicyVerdict.Allow, decision.Verdict);
        Assert.Null(decision.ExclusionReason);
        Assert.Empty(decision.Redactions);
        var disclosed = Assert.Single(outcome.DisclosedNodes);
        Assert.Equal(Text, disclosed.Text);
        Assert.Null(disclosed.DisclosedHash);
        Assert.False(disclosed.IsRedacted);
        Assert.Equal(node.ContentHash, disclosed.ContentHash);
        Assert.Equal(Text.Length, outcome.Diagnostics.DisclosedCharacterCount);
        Assert.Equal(1, outcome.Diagnostics.AllowedNodeCount);
    }

    /// <summary>
    ///     Replaces one secret line, hashes the redacted text, and records the one-based blob line.
    /// </summary>
    [Fact]
    public void Apply_RedactsSecretLineAndHashesRedactedText()
    {
        const string Secret = "AKIAABCDEFGHIJKLMNOP";
        var lines = Enumerable.Range(1, 10).Select(index => $"line {index}").ToArray();
        lines[3] = $"var apiKey = \"{Secret}\";";
        var text = string.Join('\n', lines);
        var node = ContextPolicyGraphFixtures.CreateNode("n001", "src/config.cs", text, startLine: 5);
        var service = CreateService();

        var outcome = service.Apply(ContextPolicyGraphFixtures.CreateGraph([node]), TestContext.Current.CancellationToken);

        var decision = Assert.Single(outcome.Decisions);
        Assert.Equal(ContextPolicyVerdict.Redact, decision.Verdict);
        var redaction = Assert.Single(decision.Redactions);
        Assert.Equal("aws access key id", redaction.PatternName);
        Assert.Equal(5 + 3, redaction.LineNumber);
        var disclosed = Assert.Single(outcome.DisclosedNodes);
        Assert.True(disclosed.IsRedacted);
        Assert.DoesNotContain(Secret, disclosed.Text, StringComparison.Ordinal);
        Assert.Contains("«redacted: aws access key id»", disclosed.Text, StringComparison.Ordinal);
        Assert.Equal(ContextPolicyGraphFixtures.Hash(disclosed.Text), disclosed.DisclosedHash);
        Assert.NotEqual(node.ContentHash, disclosed.DisclosedHash);
        Assert.Equal(node.ContentHash, disclosed.ContentHash);
        Assert.Equal(1, outcome.Diagnostics.RedactedNodeCount);
        Assert.Equal(1, outcome.Diagnostics.RedactedLineCount);
        Assert.Equal(1, outcome.Diagnostics.RedactionsByPattern["aws access key id"]);
    }

    /// <summary>
    ///     Excludes a node when redaction would cover more than the allowed share of its lines.
    /// </summary>
    [Fact]
    public void Apply_ExcludesNodeWhenRedactionCoversTooMuch()
    {
        const string Text = "var first = \"AKIAABCDEFGHIJKLMNOP\";\nvar second = \"AKIAQRSTUVWXYZABCDEF\";";
        var node = ContextPolicyGraphFixtures.CreateNode("n001", "src/config.cs", Text);
        var service = CreateService();

        var outcome = service.Apply(ContextPolicyGraphFixtures.CreateGraph([node]), TestContext.Current.CancellationToken);

        var decision = Assert.Single(outcome.Decisions);
        Assert.Equal(ContextPolicyVerdict.Exclude, decision.Verdict);
        Assert.Equal("redaction covers more than 0.5 of the node's lines", decision.ExclusionReason);
        Assert.Empty(outcome.DisclosedNodes);
        Assert.Equal(1, outcome.Diagnostics.ExcludedByReason["redaction covers more than 0.5 of the node's lines"]);
    }

    /// <summary>
    ///     Excludes a node over the disclosed character bound.
    /// </summary>
    [Fact]
    public void Apply_ExcludesNodeOverCharacterBound()
    {
        var node = ContextPolicyGraphFixtures.CreateNode("n001", "src/large.cs", "abcdefghijklmnopqrstuvwxyz");
        var service = CreateService(options: new ContextPolicyOptions { MaximumDisclosedCharactersPerNode = 10 });

        var outcome = service.Apply(ContextPolicyGraphFixtures.CreateGraph([node]), TestContext.Current.CancellationToken);

        var decision = Assert.Single(outcome.Decisions);
        Assert.Equal(ContextPolicyVerdict.Exclude, decision.Verdict);
        Assert.Equal("more than 10 characters", decision.ExclusionReason);
        Assert.Empty(outcome.DisclosedNodes);
    }

    /// <summary>
    ///     Excludes a node over the evidence graph quoted-line cap.
    /// </summary>
    [Fact]
    public void Apply_ExcludesNodeOverLineCap()
    {
        var node = ContextPolicyGraphFixtures.CreateNode("n001", "src/long.cs", "a\nb\nc\nd\ne");
        var service = CreateService(graphOptions: new EvidenceGraphOptions { MaximumQuotedLinesPerNode = 3 });

        var outcome = service.Apply(ContextPolicyGraphFixtures.CreateGraph([node]), TestContext.Current.CancellationToken);

        var decision = Assert.Single(outcome.Decisions);
        Assert.Equal(ContextPolicyVerdict.Exclude, decision.Verdict);
        Assert.Equal("more than 3 quoted lines", decision.ExclusionReason);
        Assert.Empty(outcome.DisclosedNodes);
    }

    /// <summary>
    ///     Excludes nodes whose paths the shared path rules reject.
    /// </summary>
    [Fact]
    public void Apply_ExcludesNodesByPath()
    {
        var environment = ContextPolicyGraphFixtures.CreateNode("n001", ".env", "KEY=value");
        var dependency = ContextPolicyGraphFixtures.CreateNode("n002", "node_modules/x.js", "module.exports = 1;");
        var service = CreateService();

        var outcome = service.Apply(
            ContextPolicyGraphFixtures.CreateGraph([environment, dependency]), TestContext.Current.CancellationToken);

        Assert.Equal(2, outcome.Decisions.Count);
        Assert.All(outcome.Decisions, decision => Assert.Equal(ContextPolicyVerdict.Exclude, decision.Verdict));
        Assert.All(outcome.Decisions, decision => Assert.StartsWith("path excluded:", decision.ExclusionReason, StringComparison.Ordinal));
        Assert.Equal("path excluded: environment file", outcome.Decisions[0].ExclusionReason);
        Assert.Equal("path excluded: inside 'node_modules'", outcome.Decisions[1].ExclusionReason);
        Assert.Empty(outcome.DisclosedNodes);
    }

    /// <summary>
    ///     Drops edges to excluded nodes and withholds a matched value that no disclosed endpoint still contains.
    /// </summary>
    [Fact]
    public void Apply_FiltersEdgesByDisclosedEndpoints()
    {
        const string Secret = "AKIAABCDEFGHIJKLMNOP";
        var changed = ContextPolicyGraphFixtures.CreateNode(
            "n001", "src/app.cs", $"var apiKey = \"{Secret}\";\nzorblaxquant", startLine: 1, isChangedFile: true);
        var candidate = ContextPolicyGraphFixtures.CreateNode("n002", "src/consumer.cs", "zorblaxquant");
        var excluded = ContextPolicyGraphFixtures.CreateNode("n003", ".env", "module.exports = 1;");
        var edges = new[]
        {
            ContextPolicyGraphFixtures.CreateEdge("e001", "n001", "n002", "zorblaxquant"),
            ContextPolicyGraphFixtures.CreateEdge("e002", "n001", "n002", Secret),
            ContextPolicyGraphFixtures.CreateEdge("e003", "n001", "n003", "anything"),
        };
        var service = CreateService();

        var outcome = service.Apply(
            ContextPolicyGraphFixtures.CreateGraph([changed, candidate, excluded], edges), TestContext.Current.CancellationToken);

        Assert.Equal(3, outcome.EdgeDecisions.Count);
        Assert.Equal(1, outcome.Diagnostics.DroppedEdgeCount);
        Assert.Equal(1, outcome.Diagnostics.WithheldMatchedValueCount);
        var disclosed = outcome.DisclosedEdges.ToDictionary(edge => edge.EdgeId, StringComparer.Ordinal);
        Assert.Equal("zorblaxquant", disclosed["e001"].MatchedValue);
        Assert.Null(disclosed["e002"].MatchedValue);
        Assert.DoesNotContain("e003", disclosed.Keys);
        Assert.False(outcome.EdgeDecisions.Single(decision => decision.EdgeId == "e003").IsDisclosed);
        Assert.True(outcome.EdgeDecisions.Single(decision => decision.EdgeId == "e002").MatchedValueWithheld);
        Assert.All(outcome.DisclosedNodes, node => Assert.DoesNotContain(Secret, node.Text, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Keeps repository paths, quoted text, and secret values out of information log entries.
    /// </summary>
    [Fact]
    public void Apply_InformationLogsOmitPathsAndSecretValues()
    {
        const string Secret = "AKIAABCDEFGHIJKLMNOP";
        var node = ContextPolicyGraphFixtures.CreateNode("n001", "src/config.cs", $"var apiKey = \"{Secret}\";\nclean");
        var logger = new RecordingSnapshotLogger<ContextPolicyService>();
        var service = CreateService(logger: logger);

        service.Apply(ContextPolicyGraphFixtures.CreateGraph([node]), TestContext.Current.CancellationToken);

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information);
        Assert.All(
            logger.Entries.Where(entry => entry.Level >= LogLevel.Information),
            entry =>
            {
                Assert.DoesNotContain("src/config.cs", entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("apiKey", entry.Message, StringComparison.Ordinal);
            });
    }

    private static ContextPolicyService CreateService(
        EvidenceGraphOptions? graphOptions = null,
        ContextPolicyOptions? options = null,
        ILogger<ContextPolicyService>? logger = null) =>
        new(
            graphOptions ?? new EvidenceGraphOptions(),
            options ?? new ContextPolicyOptions(),
            logger ?? NullLogger<ContextPolicyService>.Instance);
}
