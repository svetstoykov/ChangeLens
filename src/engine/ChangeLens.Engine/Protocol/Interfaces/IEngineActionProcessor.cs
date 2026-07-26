using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.Protocol.Interfaces;

/// <summary>
///     Defines correlated processing for validated engine protocol requests.
/// </summary>
internal interface IEngineActionProcessor
{
    /// <summary>
    ///     Asynchronously processes one validated common request envelope.
    /// </summary>
    /// <param name="request">The request to process. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the action.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains one correlated protocol
    ///     response.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<ProtocolResponse> ProcessAsync(EngineProtocolRequest request, CancellationToken cancellationToken);
}
