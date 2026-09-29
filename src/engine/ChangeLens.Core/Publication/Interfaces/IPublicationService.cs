using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Results.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Core.Publication.Interfaces;

/// <summary>Publishes validated curator output into the reading model and evidence frontier.</summary>
public interface IPublicationService
{
    /// <summary>Publishes one validated analysis.</summary>
    /// <param name="request">The publication request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The publication result.</returns>
    Task<Result<PublicationOutcome>> PublishAsync(PublicationRequest request, CancellationToken cancellationToken);

    /// <summary>Rebuilds a reading model from an already published explanation without repairing shapes or checking claims again.</summary>
    /// <param name="explanation">The published explanation of an earlier publication.</param>
    /// <param name="binder">The disclosed evidence binder.</param>
    /// <param name="graph">The complete evidence graph.</param>
    /// <param name="review">The review result to publish with the explanation.</param>
    /// <returns>The reading model.</returns>
    ReadingModel Rebuild(PublishedExplanation explanation, EvidenceBinderModel binder, EvidenceGraphModel graph, PublicationReview review);
}
