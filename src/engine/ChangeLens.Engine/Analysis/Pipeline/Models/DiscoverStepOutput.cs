using ChangeLens.Core.Analysis.ContextPolicy.Models;
using ChangeLens.Core.Analysis.Correspondence.Models;
using EvidenceBinderModel = ChangeLens.Core.Analysis.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.Analysis.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Engine.Analysis.Pipeline.Models;

/// <summary>
///     Represents the immutable handoff produced when the discover step completes.
/// </summary>
/// <param name="Ranking">The correspondence ranking discover produced. Cannot be <see langword="null" />.</param>
/// <param name="Graph">The evidence graph discover produced. Cannot be <see langword="null" />.</param>
/// <param name="Policy">The context policy outcome discover produced. Cannot be <see langword="null" />.</param>
/// <param name="Binder">The evidence binder discover assembled. Cannot be <see langword="null" />.</param>
internal sealed record DiscoverStepOutput(
    CorrespondenceRanking Ranking,
    EvidenceGraphModel Graph,
    ContextPolicyOutcome Policy,
    EvidenceBinderModel Binder);
