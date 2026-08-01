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
///     Interrupts orphaned analysis run state at startup, then takes and processes pending runs one at a time.
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
    private Task? _processorLoopTask;

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
        this._processorLoopTask = this.RunProcessorLoopAsync(this._shutdownSource.Token);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await this._shutdownSource.CancelAsync();

        if (this._processorLoopTask is not null)
        {
            try
            {
                await this._processorLoopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RunProcessorLoopAsync(CancellationToken shutdownToken)
    {
        while (!shutdownToken.IsCancellationRequested)
        {
            try
            {
                await this.FinalizeCancelledPendingRunsAsync();

                Guid? runId;
                while (!shutdownToken.IsCancellationRequested && (runId = await this.ClaimNextPendingRunAsync()) is not null)
                {
                    await this.ExecuteRunAsync(runId.Value, shutdownToken);
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

        var finalizeResult = await store.FinalizeCancelledPendingRunsAsync(timeProvider.GetUtcNow().ToUnixTimeMilliseconds(), CancellationToken.None);
        if (finalizeResult.IsFailure)
        {
            logger.LogError(
                "The analysis processor could not finalize cancelled pending runs with errors {ErrorCodes}.",
                finalizeResult.Errors.Select(error => error.Code));
        }
    }

    private async Task<Guid?> ClaimNextPendingRunAsync()
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAnalysisRunStore>();
        var takePendingAnalysis = await store.TakeNextPendingAsync(CancellationToken.None);

        if (takePendingAnalysis.IsFailure)
        {
            logger.LogError(
                "The analysis processor could not take pending work with errors {ErrorCodes}.",
                takePendingAnalysis.Errors.Select(error => error.Code));
            return null;
        }

        return takePendingAnalysis.Data;
    }

    private async Task ExecuteRunAsync(Guid runId, CancellationToken shutdownToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAnalysisRunStore>();
        var runToken = processorControl.BeginRun(runId);
        try
        {
            var getDetails = await store.GetDetailAsync(runId, CancellationToken.None);
            if (getDetails.IsFailure)
            {
                logger.LogError(
                    "The analysis processor could not re-read taken run {RunId} with errors {ErrorCodes}.",
                    runId, getDetails.Errors.Select(error => error.Code));

                return;
            }

            var runDetails = getDetails.Data!;
            if (runDetails.CancellationRequested)
            {
                processorControl.RequestRunCancellation(runId);
            }

            logger.LogInformation("Analysis processor took run {RunId}.", runId);
            var pipeline = scope.ServiceProvider.GetRequiredService<IAnalysisPipeline>();
            await pipeline.RunAsync(runId, runToken, shutdownToken);
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            logger.LogInformation("Analysis run {RunId} stopped because the engine is shutting down.", runId);
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            var terminal = new AnalysisTerminalSummary(
                AnalysisTerminalKind.Cancelled,
                timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                null,
                null);
            await this.RecordTerminalAsync(store, runId, terminal, "cancellation", LogLevel.Debug);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Analysis run {RunId} pipeline failed with an unexpected exception.", runId);
            var terminal = new AnalysisTerminalSummary(
                AnalysisTerminalKind.Failed,
                timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                null,
                AnalysisFailureCode.UnexpectedFailure);
            await this.RecordTerminalAsync(store, runId, terminal, "unexpected failure", LogLevel.Warning);
        }
        finally
        {
            processorControl.EndRun(runId);
        }
    }

    private async Task RecordTerminalAsync(
        IAnalysisRunStore store, Guid runId, AnalysisTerminalSummary terminal, string reason, LogLevel alreadyTerminalLevel)
    {
        var terminalResult = await store.CommitTerminalAsync(runId, terminal, CancellationToken.None);
        if (terminalResult.IsFailure)
        {
            logger.LogError(
                "The analysis processor could not record {Reason} for run {RunId} with errors {ErrorCodes}.",
                reason,
                runId,
                terminalResult.Errors.Select(error => error.Code));
        }
        else if (!terminalResult.Data)
        {
            logger.Log(alreadyTerminalLevel, "Analysis run {RunId} was already terminal when {Reason} was observed.", runId, reason);
        }
    }
}
