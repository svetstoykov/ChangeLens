using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.Results.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.EvidenceBinder.Interfaces;

/// <summary>
///     Defines assembly of the only context payload available to the curator.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They read only the supplied frozen inputs and do not perform
///     repository or model-provider operations.
/// </remarks>
public interface IEvidenceBinderService
{
    /// <summary>
    ///     Assembles disclosed evidence, comparison facts, orientation, and the closed curator contract.
    /// </summary>
    /// <param name="request">The frozen binder inputs. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while assembling.</param>
    /// <returns>A successful binder or an explicit budget/option failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken" /> is canceled.</exception>
    Result<EvidenceBinderModel> Assemble(EvidenceBinderRequest request, CancellationToken cancellationToken);
}
