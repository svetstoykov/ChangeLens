using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Review.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.Review.Interfaces;

/// <summary>
///     Defines the one-call reviewer that proposes findings over disclosed binder evidence.
/// </summary>
/// <remarks>
///     Implementations make one JSON completion call with no tools. A successful result contains either a parsed
///     draft or an empty draft with parse diagnostics; provider failures remain failed results from the completion port.
/// </remarks>
public interface IReviewerService
{
    /// <summary>
    ///     Asynchronously requests and parses one reviewer draft for the supplied binder.
    /// </summary>
    /// <param name="binder">The disclosed evidence binder. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while calling the provider.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a draft outcome or a provider
    ///     error.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="binder" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled <paramref name="cancellationToken" />.</exception>
    Task<Result<ReviewerOutcome>> ReviewAsync(EvidenceBinderModel binder, CancellationToken cancellationToken);
}
