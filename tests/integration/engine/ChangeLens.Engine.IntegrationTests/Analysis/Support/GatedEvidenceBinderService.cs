using ChangeLens.Core.EvidenceBinder.Interfaces;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.Results.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Wraps the real evidence binder service, announcing its entry and blocking until the supplied run token is
///     cancelled.
/// </summary>
internal sealed class GatedEvidenceBinderService : IEvidenceBinderService
{
    private readonly IEvidenceBinderService _inner;
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    ///     Initializes a new instance of the <see cref="GatedEvidenceBinderService" /> class.
    /// </summary>
    /// <param name="inner">The real binder the gate delegates to once the run token is cancelled. Cannot be
    /// <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner" /> is <see langword="null" />.</exception>
    internal GatedEvidenceBinderService(IEvidenceBinderService inner) =>
        this._inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>
    ///     Gets a task that completes once assembly has been entered.
    /// </summary>
    internal Task Entered => this._entered.Task;

    /// <inheritdoc />
    public Result<EvidenceBinderModel> Assemble(EvidenceBinderRequest request, CancellationToken cancellationToken)
    {
        this._entered.TrySetResult();
        if (!cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(60)))
        {
            throw new TimeoutException("The gated evidence binder was never cancelled.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return this._inner.Assemble(request, cancellationToken);
    }
}