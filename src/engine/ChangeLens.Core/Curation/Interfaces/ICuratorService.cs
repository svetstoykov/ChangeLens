using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.Results.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.Curation.Interfaces;

/// <summary>
///     Defines the one-call curator that turns a disclosed binder into a draft mental model.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. A successful result contains either a parsed draft or an
///     empty draft with parse diagnostics; provider failures remain failed results from the completion port.
/// </remarks>
public interface ICuratorService
{
    /// <summary>
    ///     Asynchronously requests and parses one curator draft for the supplied binder.
    /// </summary>
    /// <param name="binder">The disclosed evidence binder. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while calling the provider.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a draft outcome or a provider
    ///     error.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="binder" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled <paramref name="cancellationToken" />.</exception>
    Task<Result<CuratorOutcome>> CurateAsync(EvidenceBinderModel binder, CancellationToken cancellationToken);
}
