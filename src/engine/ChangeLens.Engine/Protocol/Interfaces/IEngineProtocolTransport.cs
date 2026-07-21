using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.Protocol.Interfaces;

/// <summary>
///     Defines bounded request input and exactly-once response output for the engine protocol.
/// </summary>
internal interface IEngineProtocolTransport
{
    /// <summary>
    ///     Asynchronously reads and decodes one protocol request.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for input.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the decoded request, successful
    ///     <see langword="null" /> when input closes, or a known transport failure.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<EngineProtocolRequest?>> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously serializes, writes, and flushes one protocol response.
    /// </summary>
    /// <param name="response">The response to write. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for output.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the output outcome.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result> WriteAsync(ProtocolResponse response, CancellationToken cancellationToken);
}
