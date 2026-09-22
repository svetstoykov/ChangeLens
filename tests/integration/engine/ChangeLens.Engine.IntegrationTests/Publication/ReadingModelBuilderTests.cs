using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceBinder.Services;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Publication.Helpers;
using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Engine.IntegrationTests.DraftValidation.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Engine.IntegrationTests.Publication;

/// <summary>Verifies the six-input reading-model boundary and duplicate citation suppression.</summary>
public sealed class ReadingModelBuilderTests
{
    /// <summary>Verifies a held quote becomes evidence and citation data through the six-input builder.</summary>
    [Fact]
    public void BuildsFromTheSixPublicationInputs()
    {
        var binder = FixtureBinder("n1");
        var graph = FixtureGraph("n1");
        var model = new MentalModel(null, [new MentalModelTrack(
            "track",
            "Track",
            new MentalModelStatement("summary", "A summary", ["n1"], []),
            CuratorContractConstants.Walk,
            [],
            [],
            [],
            [])]);
        IReadOnlyList<Citation> citations =
        [new Citation("summary", "n1", ChangeAnatomySide.After, "src/n1.cs", "blob", 1, 1, [], CitationProvenance.Unchecked)];

        var reading = ReadingModelBuilder.Build(binder.Comparison, model, citations, binder, graph, new CheckerFacts(false, false));

        Assert.Single(reading.Areas);
        Assert.Single(reading.Citations);
        Assert.Single(reading.Evidence);
        Assert.Equal("quote for n1", reading.Evidence[0].Text);
    }

    /// <summary>Verifies duplicate claim ids withhold citations for every claimant.</summary>
    [Fact]
    public void DuplicateClaimIdsProduceNoCitationForEitherClaimant()
    {
        var binder = FixtureBinder("n1", "n2");
        var graph = FixtureGraph("n1", "n2");
        var duplicate = new MentalModelStatement("duplicate", "duplicate", ["n1"], []);
        var model = new MentalModel(null, [
            new MentalModelTrack("one", "One", duplicate, CuratorContractConstants.Walk, [], [], [], []),
            new MentalModelTrack("two", "Two", duplicate, CuratorContractConstants.Walk, [], [], [], []),
        ]);
        var citations = new[]
        {
            new Citation("duplicate", "n1", ChangeAnatomySide.After, "src/n1.cs", "blob-1", 1, 1, [], CitationProvenance.Unchecked),
            new Citation("duplicate", "n2", ChangeAnatomySide.After, "src/n2.cs", "blob-2", 1, 1, [], CitationProvenance.Unchecked),
        };

        var reading = ReadingModelBuilder.Build(binder.Comparison, model, citations, binder, graph, new CheckerFacts(false, false));

        Assert.Empty(reading.Citations);
        Assert.Empty(reading.Evidence);
        Assert.Contains(reading.Assurances, assurance => assurance.Kind == ReadingAssuranceKind.DuplicateClaimId);
    }

    /// <summary>Verifies publication keeps distinct checker focus ranges on one citation without widening the gap.</summary>
    [Fact]
    public void CitationRoundTripPreservesDisjointFocusRangesAndDeduplicatesThem()
    {
        var first = new FocusRange("n1", 10, 12);
        var second = new FocusRange("n1", 40, 42);
        var binder = FixtureBinder("n1") with
        {
            Evidence = [new BinderEvidence(
                "n1",
                ChangeAnatomySide.After,
                "src/n1.cs",
                10,
                42,
                true,
                [],
                1,
                string.Join('\n', Enumerable.Range(10, 33).Select(line => $"line {line}")),
                "sha256:n1",
                null,
                false,
                false)],
        };
        var graph = FixtureGraph("n1") with
        {
            Nodes = [FixtureGraph("n1").Nodes[0] with { StartLine = 10, EndLine = 42 }],
        };
        var model = new MentalModel(null, [new MentalModelTrack(
            "track",
            "Track",
            new MentalModelStatement("summary", "Summary", ["n1"], [first, second, first]),
            CuratorContractConstants.Walk,
            [],
            [],
            [],
            [])]);

        var citations = CitationBuilder.Build(model, binder, graph, new CheckerFacts(true, false));
        var reading = ReadingModelBuilder.Build(binder.Comparison, model, citations, binder, graph, new CheckerFacts(true, false));

        var citation = Assert.Single(reading.Citations);
        Assert.Equal([first, second], citation.Focus);
        Assert.Equal(10, citation.StartLine);
        Assert.Equal(42, citation.EndLine);
    }

    /// <summary>Verifies omission totals retain their full count while exact sampled paths remain navigable.</summary>
    [Fact]
    public void SampledOmissionsPreserveTotalsAndResolveOnlyExactPaths()
    {
        var binder = FixtureBinder("n1") with
        {
            ChangedFiles = [new BinderChangedFile("binary.bin", null, SnapshotChangeCategory.Modified, "100644", "100644", [])],
            Omissions = [new BinderOmission(
                EvidenceBinderOmissionKind.FileNotRead,
                250,
                "binary",
                Enumerable.Repeat("binary.bin", 199).Append("binary.bin" + new string('x', 300)).ToArray())],
        };
        var graph = FixtureGraph("n1");

        var reading = ReadingModelBuilder.Build(
            binder.Comparison,
            MentalModel.Empty,
            [],
            binder,
            graph,
            new CheckerFacts(false, false));

        var summary = Assert.Single(reading.OmissionSummaries);
        Assert.Equal(250, summary.TotalCount);
        Assert.Equal(200, summary.SampleCount);
        Assert.Equal(199, summary.ResolvedSampleCount);
        var limitation = Assert.Single(reading.Limitations);
        Assert.Equal(ReadingLimitationKind.FileNotRead, limitation.Kind);
        Assert.Equal("binary.bin", limitation.Path);
    }

    /// <summary>Verifies excluded nodes keep node counts while their file limitation is deduplicated.</summary>
    [Fact]
    public void PolicyExcludedNodesDeduplicateFileLimitations()
    {
        var binder = FixtureBinder("n1") with
        {
            Omissions = [new BinderOmission(EvidenceBinderOmissionKind.PolicyExcluded, 2, "secret", ["n1", "n2"])],
        };
        var graph = FixtureGraph("n1", "n2");
        graph = graph with { Nodes = [graph.Nodes[0], graph.Nodes[1] with { Path = graph.Nodes[0].Path }] };

        var reading = ReadingModelBuilder.Build(binder.Comparison, MentalModel.Empty, [], binder, graph, new CheckerFacts(false, false));

        var summary = Assert.Single(reading.OmissionSummaries);
        Assert.Equal(2, summary.TotalCount);
        Assert.Equal(2, summary.SampleCount);
        Assert.Equal(2, summary.ResolvedSampleCount);
        var limitation = Assert.Single(reading.Limitations);
        Assert.Equal(ReadingLimitationKind.FileNotQuoted, limitation.Kind);
        Assert.Equal("src/n1.cs", limitation.Path);
    }

    /// <summary>Verifies the real binder cap preserves skipped-file totals and leaves shortened samples unresolved.</summary>
    [Fact]
    public void RealBinderSamplesSkippedFilesWithoutGuessingShortenedPaths()
    {
        var entries = Enumerable.Range(0, 250)
            .Select(index => new SnapshotManifestEntry(
                index == 0 ? "src/" + new string('x', 300) + ".bin" : $"src/skipped-{index:000}.bin",
                null,
                SnapshotChangeCategory.Modified,
                "100644",
                "100644",
                $"base-{index:000}",
                $"head-{index:000}"))
            .ToArray();
        var run = new AnalysisRunDetail(
            Guid.NewGuid(),
            AnalysisRunState.Collecting,
            new AnalysisRepositoryIdentity(Guid.NewGuid(), "fixture", "/fixture", "fixture", new string('h', 40)),
            new AnalysisComparisonIdentity("target", new string('t', 40), "fresh"),
            1,
            null,
            42,
            Guid.NewGuid(),
            "manifest",
            entries.Length,
            new ExcludedUncommittedCounts(0, 0, 0, 0, 0),
            false,
            null,
            null,
            null);
        var snapshot = new SnapshotCapture(
            new SnapshotManifest(
                Guid.NewGuid(),
                "manifest",
                "fixture",
                "target",
                new string('t', 40),
                new string('h', 40),
                new string('m', 40),
                entries),
            new ExcludedUncommittedCounts(0, 0, 0, 0, 0));
        var anatomy = new ChangeAnatomy(
            entries.Select(entry => new ChangedFileAnatomy(
                entry.Path,
                entry.OriginalPath,
                entry.Category,
                [],
                false,
                "binary")).ToArray(),
            new ChangeAnatomyDiagnostics(entries.Length, 0, entries.Length, 0, 0, 0, 0, new Dictionary<string, int>(),
                new Dictionary<string, int> { ["binary"] = entries.Length }));
        var request = new EvidenceBinderRequest(
            run,
            snapshot,
            new ContextPolicyOutcome([], [], [], [], new ContextPolicyDiagnostics(
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new Dictionary<string, int>(), new Dictionary<string, int>())),
            new FrozenGitTreeListing([], false),
            anatomy,
            null);

        var binderResult = new EvidenceBinderService(new EvidenceBinderOptions(), NullLogger<EvidenceBinderService>.Instance)
            .Assemble(request, CancellationToken.None);
        var binder = Assert.IsType<EvidenceBinderModel>(binderResult.Data);
        var reading = ReadingModelBuilder.Build(
            binder.Comparison,
            MentalModel.Empty,
            [],
            binder,
            FixtureGraph(),
            new CheckerFacts(false, false));

        var summary = Assert.Single(reading.OmissionSummaries);
        Assert.Equal(250, summary.TotalCount);
        Assert.Equal(200, summary.SampleCount);
        Assert.Equal(199, summary.ResolvedSampleCount);
        Assert.Equal(199, reading.Limitations.Count(limitation => limitation.Kind == ReadingLimitationKind.FileNotRead));
        Assert.DoesNotContain(reading.Limitations, limitation => limitation.Path?.Length > 256);
    }

    private static EvidenceBinderModel FixtureBinder(params string[] nodeIds) => DraftValidationFixtureBinderBuilder.Create(nodeIds);

    private static EvidenceGraphModel FixtureGraph(params string[] nodeIds)
    {
        var nodes = nodeIds.Select(nodeId => new EvidenceNode(
            nodeId,
            $"src/{nodeId}.cs",
            ChangeAnatomySide.After,
            $"blob-{nodeId}",
            1,
            1,
            $"quote for {nodeId}",
            $"sha256:{nodeId}",
            true,
            [],
            1,
            false)).ToArray();
        return new EvidenceGraphModel(nodes, [], new EvidenceGraphDiagnostics(
            nodes.Length, 0, 0, nodes.Length, 0, nodes.Length, nodes.Length, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>()));
    }
}
