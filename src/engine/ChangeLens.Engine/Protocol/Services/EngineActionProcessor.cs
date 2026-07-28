using System.Diagnostics;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Routes an approved engine action to its handler and maps the outcome to the protocol envelope.
/// </summary>
/// <remarks>
///     <para>
///         The host registers this service as a singleton and processes actions sequentially. The service depends only
///         on singleton-safe collaborators and holds no mutable state after construction.
///     </para>
///     <para>
///         The routing map is built once from the registered handlers and is never mutated afterwards. The processor
///         owns the protocol version check, handler lookup, the readiness gate, the exception boundary, and outcome
///         logging; each handler owns its own parameter binding and capability calls.
///     </para>
/// </remarks>
/// <param name="actionHandlers">The registered protocol action handlers. Cannot be <see langword="null" />.</param>
/// <param name="engineStatusService">The engine-status capability. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for action outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="InvalidOperationException">
///     A registered handler has a blank action name.
///     -or-
///     More than one handler is registered for the same action.
/// </exception>
internal sealed class EngineActionProcessor(
    IEnumerable<IActionHandler> actionHandlers,
    IEngineStatusService engineStatusService,
    ILogger<EngineActionProcessor> logger) : IEngineActionProcessor
{
    private readonly IReadOnlyDictionary<string, IActionHandler> _actionHandlersByAction = CreateActionHandlerMap(actionHandlers);

    /// <inheritdoc />
    public async Task<ProtocolResponse> ProcessAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startedAt = Stopwatch.GetTimestamp();

        ProtocolResponse response;
        try
        {
            if (request.ProtocolVersion != EngineProtocolConstants.CurrentVersion)
            {
                response = ProtocolResponseFactory.FromError(
                    request.RequestId,
                    OperationError.UnprocessableInput(
                        $"Protocol version {request.ProtocolVersion} is not supported.",
                        EngineErrorCode.UnsupportedVersion));
            }
            else
            {
                response = await this.ProcessKnownVersionAsync(request, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var loggedException = IsComparisonAction(request.Action)
                ? new InvalidOperationException("Unexpected comparison action failure.")
                : exception;
            logger.LogError(
                loggedException,
                "Unexpected failure processing engine action {RequestId} for {Action} with error {ErrorCode} in " +
                "{ElapsedMilliseconds:0.000} ms.",
                request.RequestId,
                request.Action,
                EngineErrorCode.UnexpectedFailure,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return ProtocolResponseFactory.CreateUnexpectedFailure(request.RequestId);
        }

        this.LogOutcome(response, request, Stopwatch.GetElapsedTime(startedAt));
        return response;
    }

    /// <summary>
    ///     Routes one current-version request to the handler that owns its action.
    /// </summary>
    /// <param name="request">The current-version request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the action.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the handler's response, the
    ///     readiness failure that prevented it, or the unknown-action error.
    /// </returns>
    private async Task<ProtocolResponse> ProcessKnownVersionAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        if (!this._actionHandlersByAction.TryGetValue(request.Action, out var actionHandler))
        {
            return ProtocolResponseFactory.FromError(
                request.RequestId,
                OperationError.NotFound($"The action '{request.Action}' is not recognized.", EngineErrorCode.UnknownAction));
        }

        var readinessResult = await engineStatusService.CheckStatusAsync(cancellationToken);
        return readinessResult.IsFailure
            ? ProtocolResponseFactory.FromResult(request.RequestId, readinessResult)
            : await actionHandler.HandleAsync(request, cancellationToken);
    }

    /// <summary>
    ///     Builds the immutable ordinal routing map from the registered handlers.
    /// </summary>
    /// <param name="actionHandlers">The registered action handlers. Cannot be <see langword="null" />.</param>
    /// <returns>One handler per action, keyed by its ordinal action name.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="actionHandlers" /> is <see langword="null" />, or it yields a <see langword="null" /> handler.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     A handler has a blank action name.
    ///     -or-
    ///     More than one handler is registered for the same action.
    /// </exception>
    private static IReadOnlyDictionary<string, IActionHandler> CreateActionHandlerMap(IEnumerable<IActionHandler> actionHandlers)
    {
        ArgumentNullException.ThrowIfNull(actionHandlers);

        var handlersByAction = new Dictionary<string, IActionHandler>(StringComparer.Ordinal);
        foreach (var handler in actionHandlers)
        {
            ArgumentNullException.ThrowIfNull(handler);

            if (string.IsNullOrWhiteSpace(handler.Action))
            {
                throw new InvalidOperationException($"The {handler.GetType().FullName} action handler has no action name.");
            }

            if (!handlersByAction.TryAdd(handler.Action, handler))
            {
                throw new InvalidOperationException($"More than one action handler is registered for '{handler.Action}'.");
            }
        }

        return handlersByAction;
    }

    /// <summary>
    ///     Determines whether an action belongs to the comparison boundary with restricted diagnostic context.
    /// </summary>
    /// <param name="action">The action name.</param>
    /// <returns><see langword="true" /> for an approved comparison action; otherwise, <see langword="false" />.</returns>
    private static bool IsComparisonAction(string action) =>
        action is ComparisonActionConstants.ListTargetsAction or
            ComparisonActionConstants.PrepareAction or
            ComparisonActionConstants.CheckFreshnessAction or
            ComparisonActionConstants.CheckRemoteBaselineAction or
            ComparisonActionConstants.RefreshRemoteBaselineAction;

    /// <summary>
    ///     Logs one successful or expected failed action outcome.
    /// </summary>
    /// <param name="response">The action response.</param>
    /// <param name="request">The processed request.</param>
    /// <param name="elapsed">The elapsed action-processing time.</param>
    private void LogOutcome(
        ProtocolResponse response,
        EngineProtocolRequest request,
        TimeSpan elapsed)
    {
        if (response is ProtocolErrorResponse errorResponse)
        {
            logger.LogInformation(
                "Processed engine action {RequestId} for {Action} with errors {ErrorCodes} in " +
                "{ElapsedMilliseconds:0.000} ms.",
                request.RequestId,
                request.Action,
                errorResponse.Errors.Select(error => error.Code).ToArray(),
                elapsed.TotalMilliseconds);
            return;
        }

        logger.LogInformation(
            "Processed engine action {RequestId} for {Action} with a result in {ElapsedMilliseconds:0.000} ms.",
            request.RequestId,
            request.Action,
            elapsed.TotalMilliseconds);
    }
}
