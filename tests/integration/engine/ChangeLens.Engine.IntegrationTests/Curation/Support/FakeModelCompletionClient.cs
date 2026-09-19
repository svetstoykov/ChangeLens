using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Curation.Support;

/// <summary>
///     Provides a controlled completion result for curator integration tests.
/// </summary>
internal sealed class FakeModelCompletionClient(Result<ModelCompletionModel> result) : IModelCompletionClient
{
    /// <summary>
    ///     Gets the last request received by the fake client.
    /// </summary>
    internal ModelCompletionRequest? LastRequest { get; private set; }

    /// <summary>
    ///     Gets the number of completion calls received by the fake client.
    /// </summary>
    internal int CallCount { get; private set; }

    /// <inheritdoc />
    public Task<Result<ModelCompletionModel>> CompleteJsonAsync(ModelCompletionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        this.LastRequest = request;
        this.CallCount++;
        return Task.FromResult(result);
    }
}
