using System.Collections.Concurrent;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Review.Services;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Routes each provider call to the curator or the reviewer responder by its system message and counts the calls it
///     receives. The client is safe for the two concurrent calls the collect step makes.
/// </summary>
internal sealed class ScriptedModelCompletionClient : IModelCompletionClient
{
    private readonly Func<ModelCompletionRequest, CancellationToken, Task<Result<ModelCompletionModel>>> _curatorResponder;
    private readonly Func<ModelCompletionRequest, CancellationToken, Task<Result<ModelCompletionModel>>>? _reviewerResponder;
    private readonly ConcurrentQueue<ModelCompletionRequest> _requests = new();
    private ModelCompletionRequest? _lastRequest;
    private int _curatorCallCount;
    private int _reviewerCallCount;

    /// <summary>
    ///     Initializes a client whose responders answer immediately.
    /// </summary>
    /// <param name="curatorResponder">The curator completion factory. Cannot be <see langword="null" />.</param>
    /// <param name="reviewerResponder">
    ///     The reviewer completion factory, or <see langword="null" /> when a reviewer call is a test failure.
    /// </param>
    internal ScriptedModelCompletionClient(
        Func<ModelCompletionRequest, Result<ModelCompletionModel>> curatorResponder,
        Func<ModelCompletionRequest, Result<ModelCompletionModel>>? reviewerResponder = null)
        : this(
            (request, _) => Task.FromResult(curatorResponder(request)),
            reviewerResponder is null ? null : (request, _) => Task.FromResult(reviewerResponder(request)))
    {
    }

    /// <summary>
    ///     Initializes a client whose responders may wait, for example on the other call or on cancellation.
    /// </summary>
    /// <param name="curatorResponder">The curator completion factory. Cannot be <see langword="null" />.</param>
    /// <param name="reviewerResponder">
    ///     The reviewer completion factory, or <see langword="null" /> when a reviewer call is a test failure.
    /// </param>
    internal ScriptedModelCompletionClient(
        Func<ModelCompletionRequest, CancellationToken, Task<Result<ModelCompletionModel>>> curatorResponder,
        Func<ModelCompletionRequest, CancellationToken, Task<Result<ModelCompletionModel>>>? reviewerResponder)
    {
        this._curatorResponder = curatorResponder;
        this._reviewerResponder = reviewerResponder;
    }

    /// <summary>
    ///     Gets the last request received by the client.
    /// </summary>
    internal ModelCompletionRequest? LastRequest => Volatile.Read(ref this._lastRequest);

    /// <summary>
    ///     Gets every request received, in arrival order.
    /// </summary>
    internal IReadOnlyCollection<ModelCompletionRequest> Requests => this._requests;

    /// <summary>
    ///     Gets the number of completion calls received by the client.
    /// </summary>
    internal int CallCount => this.CuratorCallCount + this.ReviewerCallCount;

    /// <summary>
    ///     Gets the number of curator calls received by the client.
    /// </summary>
    internal int CuratorCallCount => Volatile.Read(ref this._curatorCallCount);

    /// <summary>
    ///     Gets the number of reviewer calls received by the client.
    /// </summary>
    internal int ReviewerCallCount => Volatile.Read(ref this._reviewerCallCount);

    /// <inheritdoc />
    public Task<Result<ModelCompletionModel>> CompleteJsonAsync(ModelCompletionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref this._lastRequest, request);
        this._requests.Enqueue(request);
        if (!request.SystemMessage.StartsWith(ReviewerSystemMessage.RoleLine, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref this._curatorCallCount);
            return this._curatorResponder(request, cancellationToken);
        }

        Interlocked.Increment(ref this._reviewerCallCount);
        return this._reviewerResponder?.Invoke(request, cancellationToken)
            ?? throw new InvalidOperationException("The scripted client received a reviewer call but has no reviewer responder.");
    }
}
