using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.Snapshots.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Engine.IntegrationTests.FindingValidation.Support;

/// <summary>
///     Builds controlled binders with source quotes and manifest facts for finding-validation tests.
/// </summary>
internal static class FindingValidationFixtureBinderBuilder
{
    /// <summary>
    ///     Creates a binder with the supplied evidence nodes.
    /// </summary>
    /// <param name="evidence">The evidence nodes to disclose.</param>
    /// <returns>A binder containing only controlled fixture data.</returns>
    internal static EvidenceBinderModel Create(params BinderEvidence[] evidence)
    {
        var contract = new BinderContract(
            CuratorContractConstants.RelationshipKinds,
            CuratorContractConstants.TrackShapes,
            new CuratorLimits(8, 8, 8, 8, 200, CuratorContractConstants.IdFormat));
        return new EvidenceBinderModel(
            new BinderComparison(
                Guid.Empty,
                "fixture",
                "target",
                "target-revision",
                "head-revision",
                "merge-base",
                0,
                new ExcludedUncommittedCounts(0, 0, 0, 0, 0)),
            null,
            [],
            evidence,
            [],
            new BinderOrientation([]),
            contract,
            [],
            new BinderDiagnostics(0, 0, 0, 0, false, [], evidence.Length, 0, 0, 0, 0, 0, 0, []));
    }

    /// <summary>
    ///     Creates a source quote with a controlled line range and changed-file flag.
    /// </summary>
    /// <param name="nodeId">The evidence node identifier.</param>
    /// <param name="text">The quoted source text.</param>
    /// <param name="startLine">The one-based line containing the first quote line.</param>
    /// <param name="isChangedFile">Whether the quote belongs to a changed file.</param>
    /// <returns>A source quote suitable for a finding anchor.</returns>
    internal static BinderEvidence SourceEvidence(string nodeId, string text, int startLine = 10, bool isChangedFile = true)
    {
        var lineCount = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').Length;
        return new BinderEvidence(
            nodeId,
            ChangeAnatomySide.After,
            $"src/{nodeId}.cs",
            startLine,
            startLine + lineCount - 1,
            isChangedFile,
            [],
            1,
            text,
            $"sha256:{nodeId}",
            null,
            false,
            false);
    }

    /// <summary>
    ///     Creates a manifest fact that has no source line range.
    /// </summary>
    /// <param name="nodeId">The manifest evidence node identifier.</param>
    /// <returns>A manifest fact suitable for testing anchor rejection.</returns>
    internal static BinderEvidence ManifestEvidence(string nodeId) => new(
        nodeId,
        ChangeAnatomySide.After,
        "src/renamed.cs",
        0,
        0,
        true,
        [],
        1,
        "manifest fact (Renamed) — src/old.cs → src/renamed.cs",
        $"sha256:{nodeId}",
        null,
        false,
        false);
}
