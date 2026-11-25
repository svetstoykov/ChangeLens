using ChangeLens.Core.Analysis.ContextPolicy.Models;
using ChangeLens.Core.Analysis.Correspondence.Models;
using ChangeLens.Core.Analysis.DraftValidation.Models;
using EvidenceBinderModel = ChangeLens.Core.Analysis.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.Analysis.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.Analysis.Publication.Models;

/// <summary>Collects the seven upstream publication inputs and frontier context.</summary>
/// <param name="Validation">The mechanically validated draft.</param>
/// <param name="Binder">The disclosed evidence binder.</param>
/// <param name="Graph">The complete evidence graph.</param>
/// <param name="Policy">The context-policy outcome.</param>
/// <param name="Ranking">The correspondence ranking.</param>
/// <param name="Review">The review result.</param>
public sealed record PublicationRequest(
    DraftValidationOutcome Validation,
    EvidenceBinderModel Binder,
    EvidenceGraphModel Graph,
    ContextPolicyOutcome Policy,
    CorrespondenceRanking Ranking,
    PublicationReview Review);
