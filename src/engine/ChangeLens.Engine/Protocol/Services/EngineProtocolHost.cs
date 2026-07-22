using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Hosting.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Orchestrates the engine protocol transport and typed action processor for the host lifetime.
/// </summary>
/// <remarks>
///     The Generic Host owns this singleton service. It processes requests sequentially and contains no action
///     selection, JSON, or text input/output behavior.
/// </remarks>
/// <param name="protocolTransport">The bounded protocol transport. Cannot be <see langword="null" />.</param>
/// <param name="actionProcessor">The typed action processor. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for protocol lifecycle events. Cannot be <see langword="null" />.</param>
/// <param name="applicationLifetime">
///     The application lifetime used to stop the process. Cannot be <see langword="null" />.
/// </param>
internal sealed class EngineProtocolHost(
    IEngineProtocolTransport protocolTransport,
    EngineActionProcessor actionProcessor,
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

                var response = readResult.IsFailure
                    ? ProtocolResponseFactory.CreateError(null, readResult.Errors)
                    : await actionProcessor.ProcessAsync(readResult.Data!, stoppingToken);
                var writeResult = await protocolTransport.WriteAsync(response, stoppingToken);

                if (writeResult.IsFailure)
                {
                    FailProcess(writeResult, "Engine protocol host stopped after protocol output failed.");
                    break;
                }

                if (readResult.IsFailure && !CanContinue(readResult))
                {
                    FailProcess(readResult, "Engine protocol host stopped after protocol input failed.");
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
    ///     Determines whether a rejected request leaves the transport safe for another read.
    /// </summary>
    /// <param name="readResult">The failed input result.</param>
    /// <returns>
    ///     <see langword="true" /> when every error describes rejected caller input; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool CanContinue(Result readResult) =>
        readResult.Errors.All(
            error => error.Type is ErrorType.MalformedInput or ErrorType.Validation);

    /// <summary>
    ///     Marks a transport failure as fatal and records its stable error codes.
    /// </summary>
    /// <param name="result">The fatal transport result.</param>
    /// <param name="message">The lifecycle message to record.</param>
    private void FailProcess(Result result, string message)
    {
        Environment.ExitCode = EngineProcessConstants.UnexpectedFailureExitCode;
        logger.LogCritical(
            "{FailureMessage} Errors: {ErrorCodes}",
            message,
            result.Errors.Select(error => error.Code).ToArray());
    }
}
