using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.Publication.Models;

/// <summary>Collects the six upstream publication inputs and frontier context.</summary>
/// <param name="Validation">The mechanically validated draft.</param>
/// <param name="Binder">The disclosed evidence binder.</param>
/// <param name="Graph">The complete evidence graph.</param>
/// <param name="Policy">The context-policy outcome.</param>
/// <param name="Ranking">The correspondence ranking.</param>
public sealed record PublicationRequest(
    DraftValidationOutcome Validation,
    EvidenceBinderModel Binder,
    EvidenceGraphModel Graph,
    ContextPolicyOutcome Policy,
    CorrespondenceRanking Ranking);
