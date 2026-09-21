using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Core.ModelCompletion.Interfaces;

/// <summary>
///     Defines the provider-neutral port for one JSON model completion.
/// </summary>
/// <remarks>
///     The request shape is fixed to one system instruction and one user payload, so callers cannot add conversation
///     turns to this port.
/// </remarks>
public interface IModelCompletionClient
{
    /// <summary>
    ///     Asynchronously requests one JSON completion.
    /// </summary>
    /// <param name="request">The completion request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the provider.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the completion or a provider
    ///     error.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled <paramref name="cancellationToken" />.</exception>
    Task<Result<ModelCompletionModel>> CompleteJsonAsync(ModelCompletionRequest request, CancellationToken cancellationToken);
}
