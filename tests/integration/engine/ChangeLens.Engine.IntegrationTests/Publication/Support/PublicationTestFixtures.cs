using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Engine.IntegrationTests.DraftValidation.Support;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Engine.IntegrationTests.Publication.Support;

/// <summary>
///     Builds small binders, graphs, and drafts for publication integration tests.
/// </summary>
internal static class PublicationTestFixtures
{
    /// <summary>
    ///     Creates a binder holding the supplied disclosed nodes.
    /// </summary>
    /// <param name="nodeIds">The evidence node identifiers to disclose.</param>
    /// <returns>A binder containing only controlled fixture data.</returns>
    internal static EvidenceBinderModel Binder(params string[] nodeIds) => DraftValidationFixtureBinderBuilder.Create(nodeIds);

    /// <summary>
    ///     Creates an evidence graph whose nodes match the publication binder fixture.
    /// </summary>
    /// <param name="nodeIds">The evidence node identifiers to include.</param>
    /// <returns>A graph containing only controlled fixture data.</returns>
    internal static EvidenceGraphModel Graph(params string[] nodeIds)
    {
        var nodes = nodeIds.Select(nodeId => new EvidenceNode(
            nodeId, $"src/{nodeId}.cs", ChangeAnatomySide.After, $"blob-{nodeId}", 1, 1, $"quote for {nodeId}",
            $"sha256:{nodeId}", true, [], 1, false)).ToArray();
        return new EvidenceGraphModel(nodes, [], new EvidenceGraphDiagnostics(
            nodes.Length, 0, 0, nodes.Length, 0, nodes.Length, nodes.Length, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>()));
    }

    /// <summary>
    ///     Creates an empty context-policy outcome.
    /// </summary>
    /// <returns>A policy outcome with no decisions or disclosed nodes.</returns>
    internal static ContextPolicyOutcome Policy() => new(
        [], [], [], [], new ContextPolicyDiagnostics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(), new Dictionary<string, int>()));

    /// <summary>
    ///     Creates an empty correspondence ranking.
    /// </summary>
    /// <returns>A ranking with no candidates.</returns>
    internal static CorrespondenceRanking Ranking() => new(
        [], new CorrespondenceDiagnostics(0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>()));

    /// <summary>
    ///     Creates a validated Walk draft that cites the supplied nodes.
    /// </summary>
    /// <param name="nodeIds">The evidence node identifiers cited by the thesis and summary.</param>
    /// <returns>A validation outcome containing one Walk track.</returns>
    internal static DraftValidationOutcome Validation(params string[] nodeIds)
    {
        var thesis = new BoundStatement("Thesis", nodeIds);
        var summary = new BoundStatement("Summary", nodeIds);
        var track = new DraftTrack("track", "Track", summary, CuratorContractConstants.Walk, [], [], [], []);
        return new DraftValidationOutcome(new MentalModelDraft(thesis, [track], []), [], 0, 0, 0, 0);
    }

    /// <summary>
    ///     Creates an empty claim-checking summary.
    /// </summary>
    /// <returns>A summary with all counts at zero.</returns>
    internal static ClaimCheckingSummary Summary() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, [], [], []);
}
