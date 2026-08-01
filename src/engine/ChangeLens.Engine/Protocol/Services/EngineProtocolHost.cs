using System.Diagnostics;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Hosting.Constants;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Orchestrates the engine protocol transport and routes approved actions for the host lifetime.
/// </summary>
/// <remarks>
///     <para>
///         The Generic Host owns this singleton service and processes requests sequentially.
///     </para>
///     <para>
///         Each accepted request is handled in one asynchronous service scope. The host owns the protocol version
///         check, approved-action gate, exception boundary, and outcome logging; each handler owns its parameter
///         binding and capability calls.
///     </para>
/// </remarks>
/// <param name="protocolTransport">The bounded protocol transport. Cannot be <see langword="null" />.</param>
/// <param name="serviceScopeFactory">The factory that creates request scopes. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for protocol lifecycle and action outcomes. Cannot be <see langword="null" />.</param>
/// <param name="applicationLifetime">
///     The application lifetime used to stop the process. Cannot be <see langword="null" />.
/// </param>
internal sealed class EngineProtocolHost(
    IEngineProtocolTransport protocolTransport,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<EngineProtocolHost> logger,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    /// <inheritdoc />
    /// <remarks>
    ///     Processes one request and writes exactly one response before reading the next request. Normal shutdown
    ///     cancellation and closed input stop the application successfully; fatal transport failures set a nonzero
    ///     process exit code.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Engine protocol host started and is awaiting standard input.");

            while (true)
            {
                var readResult = await protocolTransport.ReadAsync(stoppingToken);
                if (readResult.IsSuccess && readResult.Data is null)
                {
                    logger.LogInformation("Engine protocol host stopped after standard input closed.");
                    break;
                }

                ProtocolResponse response;
                if (readResult.IsFailure)
                {
                    response = ProtocolResponseFactory.CreateError(null, readResult.Errors);
                }
                else
                {
                    await using var requestScope = serviceScopeFactory.CreateAsyncScope();
                    response = await this.ProcessAsync(readResult.Data!, requestScope.ServiceProvider, stoppingToken);
                }

                var writeResult = await protocolTransport.WriteAsync(response, stoppingToken);
                if (writeResult.IsFailure)
                {
                    this.FailProcess(writeResult, "Engine protocol host stopped after protocol output failed.");
                    break;
                }

                if (readResult.IsFailure && !CanContinue(readResult))
                {
                    this.FailProcess(readResult, "Engine protocol host stopped after protocol input failed.");
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Engine protocol host stopped after application shutdown was requested.");
        }
        catch (Exception exception)
        {
            Environment.ExitCode = EngineProcessConstants.UnexpectedFailureExitCode;
            logger.LogCritical(exception, EngineProcessConstants.UnexpectedTerminationLogMessage);
        }
        finally
        {
            applicationLifetime.StopApplication();
        }
    }

    /// <summary>
    ///     Asynchronously processes one validated common request envelope.
    /// </summary>
    /// <param name="request">The request to process. Cannot be <see langword="null" />.</param>
    /// <param name="requestServices">The request-scoped service provider. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the action.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains one correlated protocol
    ///     response.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="request" /> or <paramref name="requestServices" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    internal async Task<ProtocolResponse> ProcessAsync(EngineProtocolRequest request, IServiceProvider requestServices,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(requestServices);
        var startedAt = Stopwatch.GetTimestamp();

        ProtocolResponse response;
        try
        {
            if (request.ProtocolVersion != EngineProtocolConstants.CurrentVersion)
            {
                response = ProtocolResponseFactory.FromError(
                    request.RequestId, UnsupportedProtocolVersionError(request.ProtocolVersion));
            }
            else
            {
                response = await this.ProcessKnownVersionAsync(request, requestServices, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected failure processing engine action {RequestId} for {Action} with error {ErrorCode} in " +
                "{ElapsedMilliseconds:0.000} ms.", request.RequestId, request.Action, EngineErrorCode.UnexpectedFailure,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return ProtocolResponseFactory.CreateUnexpectedFailure(request.RequestId);
        }

        this.LogOutcome(response, request, Stopwatch.GetElapsedTime(startedAt));
        return response;
    }

    /// <summary>
    ///     Routes one current-version request to the handler that owns its approved action.
    /// </summary>
    /// <param name="request">The current-version request. Cannot be <see langword="null" />.</param>
    /// <param name="requestServices">The request-scoped service provider. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the action.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the handler's response or the
    ///     unknown-action error.
    /// </returns>
    private async Task<ProtocolResponse> ProcessKnownVersionAsync(EngineProtocolRequest request, IServiceProvider requestServices,
        CancellationToken cancellationToken)
    {
        if (!EngineActionConstants.ApprovedActions.Contains(request.Action, StringComparer.Ordinal))
        {
            return ProtocolResponseFactory.FromError(request.RequestId, UnknownActionError(request.Action));
        }

        var actionHandler = (IActionHandler)requestServices.GetRequiredKeyedService(typeof(IActionHandler), request.Action);
        return await actionHandler.HandleAsync(request, cancellationToken);
    }

    /// <summary>
    ///     Marks a transport failure as fatal and records its stable error codes.
    /// </summary>
    /// <param name="result">The fatal transport result.</param>
    /// <param name="message">The lifecycle message to record.</param>
    private void FailProcess(Result result, string message)
    {
        Environment.ExitCode = EngineProcessConstants.UnexpectedFailureExitCode;
        logger.LogCritical("{FailureMessage} Errors: {ErrorCodes}", message, result.Errors.Select(error => error.Code));
    }

    /// <summary>
    ///     Logs one successful or expected failed action outcome.
    /// </summary>
    /// <param name="response">The action response.</param>
    /// <param name="request">The processed request.</param>
    /// <param name="elapsed">The elapsed action-processing time.</param>
    private void LogOutcome(ProtocolResponse response, EngineProtocolRequest request, TimeSpan elapsed)
    {
        if (response is ProtocolErrorResponse errorResponse)
        {
            logger.Log(LogLevel.Warning, "Processed engine action {RequestId} for {Action} with errors {ErrorCodes} in " +
                "{ElapsedMilliseconds:0.000} ms.", request.RequestId, request.Action, errorResponse.Errors.Select(error => error.Code),
                elapsed.TotalMilliseconds);
            return;
        }

        logger.LogInformation("Processed engine action {RequestId} for {Action} with a result in {ElapsedMilliseconds:0.000} ms.", request.RequestId,
            request.Action, elapsed.TotalMilliseconds);
    }

    private static OperationError UnsupportedProtocolVersionError(int protocolVersion) =>
        OperationError.UnprocessableInput($"Protocol version {protocolVersion} is not supported.", EngineErrorCode.UnsupportedVersion);

    private static OperationError UnknownActionError(string action) =>
        OperationError.NotFound($"The action '{action}' is not recognized.", EngineErrorCode.UnknownAction);

    /// <summary>
    ///     Determines whether a rejected request leaves the transport safe for another read.
    /// </summary>
    /// <param name="readResult">The failed input result.</param>
    /// <returns>
    ///     <see langword="true" /> when every error describes rejected caller input; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool CanContinue(Result readResult) =>
        readResult.Errors.All(error => error.Type is ErrorType.MalformedInput or ErrorType.Validation);
}
