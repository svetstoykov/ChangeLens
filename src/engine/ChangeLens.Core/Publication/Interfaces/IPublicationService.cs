using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Publication.Interfaces;

/// <summary>Publishes validated curator output into the reading model and evidence frontier.</summary>
public interface IPublicationService
{
    /// <summary>Publishes one validated analysis.</summary>
    /// <param name="request">The publication request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The publication result.</returns>
    Task<Result<PublicationOutcome>> PublishAsync(PublicationRequest request, CancellationToken cancellationToken);
}
