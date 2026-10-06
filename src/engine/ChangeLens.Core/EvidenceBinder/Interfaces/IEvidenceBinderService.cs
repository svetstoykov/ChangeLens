using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.Results.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.EvidenceBinder.Interfaces;

/// <summary>
///     Defines assembly of the only context payload available to the curator and the reviewer.
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

    /// <summary>
    ///     Derives a binder that keeps only the evidence quoted from changed files.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The reviewer judges the change itself, so it reads this narrower binder while the curator keeps the full one.
    ///         Node ids are unchanged, so every retained node resolves in the full binder too.
    ///     </para>
    ///     <para>
    ///         Match edges that touch a dropped node are removed, and the dropped nodes are recorded as one
    ///         <c>outsideChangedFiles</c> omission. Changed files, orientation, the contract, and earlier omissions are kept.
    ///     </para>
    /// </remarks>
    /// <param name="binder">The assembled binder. Cannot be <see langword="null" />.</param>
    /// <returns>The changed-file binder, with ranks and size diagnostics measured again.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="binder" /> is <see langword="null" />.</exception>
    EvidenceBinderModel ScopeToChangedFiles(EvidenceBinderModel binder);
}
