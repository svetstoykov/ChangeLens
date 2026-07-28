using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.Protocol.Interfaces;

/// <summary>
///     Defines the implementation of one approved engine protocol action.
/// </summary>
/// <remarks>
///     <para>
///         Each implementation owns exactly one action. It binds that action's parameters, calls its approved
///         capability entry point, and maps the outcome to one correlated <see cref="ProtocolResponse" />.
///     </para>
///     <para>
///         The host registers every implementation as a singleton <see cref="IActionHandler" /> service, so an
///         implementation must be stateless. Request-specific state belongs in local variables, never on the handler.
///     </para>
/// </remarks>
internal interface IActionHandler
{
    /// <summary>
    ///     Gets the protocol action this handler owns.
    /// </summary>
    /// <value>The capability-owned action constant. Never <see langword="null" /> or blank.</value>
    string Action { get; }

    /// <summary>
    ///     Asynchronously executes this handler's action for one validated request.
    /// </summary>
    /// <param name="request">The current-version request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the action.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains one correlated protocol response.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken);
}
