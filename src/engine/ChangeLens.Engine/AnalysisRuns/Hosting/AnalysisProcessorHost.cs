using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.AnalysisRuns.Hosting;

/// <summary>
///     Interrupts orphaned analysis run state at startup, then claims and processes pending runs one at a time.
/// </summary>
/// <remarks>
///     <para>
///         This singleton hosted service starts before <c>EngineProtocolHost</c>, so its recovery completes before
///         protocol requests can be read.
///     </para>
///     <para>
///         It retains bounded task ownership and shutdown state only. Wake-up and cancellation signalling belongs to
///         <see cref="IAnalysisProcessorControl" />, and each run resolves scoped services from a fresh scope.
///     </para>
/// </remarks>
/// <param name="processorControl">The processor wake-up and cancellation control. Cannot be <see langword="null" />.</param>
/// <param name="serviceScopeFactory">The factory that creates per-run async scopes. Cannot be <see langword="null" />.</param>
/// <param name="timeProvider">The time provider used for recovery and terminal timestamps. Cannot be <see langword="null" />.</param>
/// <param name="logger">The processor lifecycle logger. Cannot be <see langword="null" />.</param>
internal sealed class AnalysisProcessorHost(
    IAnalysisProcessorControl processorControl,
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<AnalysisProcessorHost> logger) : IHostedService
{
    private readonly CancellationTokenSource _shutdownSource = new();
    private Task? _claimLoopTask;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using (var recoveryScope = serviceScopeFactory.CreateAsyncScope())
        {
            var store = recoveryScope.ServiceProvider.GetRequiredService<IAnalysisRunStore>();
            var interruptedResult = await store.InterruptActiveRunsAsync(
                timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                cancellationToken);

            if (interruptedResult.IsFailure)
            {
                logger.LogCritical(
                    "Analysis processor startup recovery failed with errors {ErrorCodes}.",
                    interruptedResult.Errors.Select(error => error.Code));
                throw new InvalidOperationException(
                    "Analysis processor recovery failed; the engine cannot start with ambiguous run state. Errors: " +
                    string.Join(", ", interruptedResult.Errors.Select(error => error.Code)) + ".");
            }
        }

        logger.LogInformation("The analysis processor completed startup recovery.");
        this._claimLoopTask = this.RunClaimLoopAsync(this._shutdownSource.Token);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await this._shutdownSource.CancelAsync();

        if (this._claimLoopTask is not null)
        {
            try
            {
                await this._claimLoopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RunClaimLoopAsync(CancellationToken shutdownToken)
    {
        while (!shutdownToken.IsCancellationRequested)
        {
            try
            {
                await this.FinalizeCancelledPendingRunsAsync();

                while (!shutdownToken.IsCancellationRequested && await this.ClaimAndRunOnceAsync(shutdownToken))
                {
                }
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The analysis processor loop failed for one iteration and will retry.");
            }

            await this.WaitForNextIterationAsync(shutdownToken);
        }
    }

    private async Task WaitForNextIterationAsync(CancellationToken shutdownToken)
    {
        using var fallbackSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        fallbackSource.CancelAfter(AnalysisProcessorConstants.DatabaseFallbackInterval);

        try
        {
            await processorControl.WaitForPendingWorkAsync(fallbackSource.Token);
        }
        catch (OperationCanceledException) when (!shutdownToken.IsCancellationRequested)
        {
        }
    }

    private async Task FinalizeCancelledPendingRunsAsync()
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAnalysisRunStore>();
        var finalizeResult = await store.FinalizeCancelledPendingRunsAsync(
            timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            CancellationToken.None);
        if (finalizeResult.IsFailure)
        {
            logger.LogError(
                "The analysis processor could not finalize cancelled pending runs with errors {ErrorCodes}.",
                finalizeResult.Errors.Select(error => error.Code));
            throw new InvalidOperationException(
                "The analysis processor could not finalize cancelled pending runs. Errors: " +
                string.Join(", ", finalizeResult.Errors.Select(error => error.Code)) + ".");
        }
    }

    private async Task<bool> ClaimAndRunOnceAsync(CancellationToken shutdownToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAnalysisRunStore>();
        var claimResult = await store.ClaimNextPendingAsync(CancellationToken.None);

        if (claimResult.IsFailure)
        {
            logger.LogError(
                "The analysis processor could not claim pending work with errors {ErrorCodes}.",
                claimResult.Errors.Select(error => error.Code));
            throw new InvalidOperationException(
                "The analysis processor could not claim pending work. Errors: " +
                string.Join(", ", claimResult.Errors.Select(error => error.Code)) + ".");
        }

        if (claimResult.Data is null)
        {
            return false;
        }

        var claim = claimResult.Data;
        var runToken = processorControl.BeginRun(claim.RunId);
        try
        {
            var freshProjection = await store.GetDetailAsync(claim.RunId, CancellationToken.None);
            if (freshProjection.IsFailure)
            {
                logger.LogError(
                    "The analysis processor could not re-read claimed run {RunId} with errors {ErrorCodes}.",
                    claim.RunId,
                    freshProjection.Errors.Select(error => error.Code));
                throw new InvalidOperationException(
                    "The analysis processor could not re-read claimed run. Errors: " +
                    string.Join(", ", freshProjection.Errors.Select(error => error.Code)) + ".");
            }

            if (freshProjection.Data!.CancellationRequested)
            {
                processorControl.RequestRunCancellation(claim.RunId);
            }

            logger.LogInformation("Analysis processor claimed run {RunId}.", claim.RunId);
            var pipeline = scope.ServiceProvider.GetRequiredService<IAnalysisPipeline>();
            await pipeline.RunAsync(claim.RunId, claim.Checks, runToken, shutdownToken);
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            logger.LogInformation("Analysis run {RunId} stopped because the engine is shutting down.", claim.RunId);
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            var terminal = new AnalysisTerminalSummary(
                AnalysisTerminalKind.Cancelled,
                timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                null,
                null);
            var cancellationResult = await store.CommitTerminalAsync(claim.RunId, terminal, CancellationToken.None);
            if (cancellationResult.IsFailure)
            {
                logger.LogError(
                    "The analysis processor could not record cancellation for run {RunId} with errors {ErrorCodes}.",
                    claim.RunId,
                    cancellationResult.Errors.Select(error => error.Code));
                throw new InvalidOperationException(
                    "The analysis processor could not record cancellation. Errors: " +
                    string.Join(", ", cancellationResult.Errors.Select(error => error.Code)) + ".");
            }

            if (!cancellationResult.Data!)
            {
                logger.LogDebug("Analysis run {RunId} was already terminal when cancellation was observed.", claim.RunId);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Analysis run {RunId} pipeline failed with an unexpected exception.", claim.RunId);
            var terminal = new AnalysisTerminalSummary(
                AnalysisTerminalKind.Failed,
                timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                null,
                AnalysisFailureCode.UnexpectedFailure);
            var terminalResult = await store.CommitTerminalAsync(claim.RunId, terminal, CancellationToken.None);
            if (terminalResult.IsFailure)
            {
                logger.LogError(
                    "The analysis processor could not record unexpected failure for run {RunId} with errors {ErrorCodes}.",
                    claim.RunId,
                    terminalResult.Errors.Select(error => error.Code));
                throw new InvalidOperationException(
                    "The analysis processor could not record unexpected failure. Errors: " +
                    string.Join(", ", terminalResult.Errors.Select(error => error.Code)) + ".");
            }

            if (!terminalResult.Data!)
            {
                logger.LogWarning("Analysis run {RunId} was already terminal when an unexpected failure was observed.", claim.RunId);
            }
        }
        finally
        {
            processorControl.EndRun(claim.RunId);
        }

        return true;
    }
}
