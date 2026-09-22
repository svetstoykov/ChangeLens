using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Provides a model completion client that announces its entry and then waits until the caller cancels it.
/// </summary>
internal sealed class CancellationAwaitingModelCompletionClient : IModelCompletionClient
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    ///     Gets a task that completes once a completion call has been entered.
    /// </summary>
    internal Task Entered => this._entered.Task;

    /// <inheritdoc />
    public async Task<Result<ModelCompletionModel>> CompleteJsonAsync(ModelCompletionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        this._entered.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("The cancellation-awaiting completion client resumed without a cancellation.");
    }
}