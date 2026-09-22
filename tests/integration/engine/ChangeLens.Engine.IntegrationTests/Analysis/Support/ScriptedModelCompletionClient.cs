using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Provides a scripted completion for each provider call while counting the calls it receives.
/// </summary>
/// <param name="responder">The per-call completion factory. Cannot be <see langword="null" />.</param>
internal sealed class ScriptedModelCompletionClient(Func<ModelCompletionRequest, Result<ModelCompletionModel>> responder)
    : IModelCompletionClient
{
    /// <summary>
    ///     Gets the last request received by the client.
    /// </summary>
    internal ModelCompletionRequest? LastRequest { get; private set; }

    /// <summary>
    ///     Gets the number of completion calls received by the client.
    /// </summary>
    internal int CallCount { get; private set; }

    /// <inheritdoc />
    public Task<Result<ModelCompletionModel>> CompleteJsonAsync(ModelCompletionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        this.LastRequest = request;
        this.CallCount++;
        return Task.FromResult(responder(request));
    }
}