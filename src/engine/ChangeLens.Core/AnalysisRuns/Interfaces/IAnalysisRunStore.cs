using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.AnalysisRuns.Interfaces;

/// <summary>
///     Defines the explicit atomic durable lifecycle operations for analysis runs.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one request and do not need to be
///     thread-safe. This interface exposes explicit atomic operations rather than a generic load-mutate-save
///     aggregate API, and analysis state is not split across independent queue, lock, and run stores.
/// </remarks>
public interface IAnalysisRunStore
{
    /// <summary>
    ///     Asynchronously and atomically creates a pending run for the accepted repository, or returns the racing
    ///     active run.
    /// </summary>
    /// <param name="acceptance">The immutable acceptance request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the durable start outcome.</returns>
    Task<Result<AnalysisStartOutcome>> CreateOrReturnActiveAsync(AnalysisRunAcceptance acceptance, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously reads the current detail for one run.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the detail, or a failure with <c>analysis.unknownRun</c>.</returns>
    Task<Result<AnalysisRunDetail>> GetDetailAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously reads the active run detail for a canonical repository, if one exists.
    /// </summary>
    /// <param name="canonicalRepositoryPathKey">The canonical repository path key. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the active detail, or <see langword="null" /> when none exists.</returns>
    Task<Result<AnalysisRunDetail?>> GetActiveByRepositoryAsync(string canonicalRepositoryPathKey, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously and atomically takes the oldest pending run that carries no durable cancellation request.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the taken run identifier, or <see langword="null" /> when none is available.</returns>
    Task<Result<Guid?>> TakeNextPendingAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously establishes the deterministic step plan for a taken run.
    /// </summary>
    /// <param name="runId">The taken run identifier.</param>
    /// <param name="plan">The ordered step-plan entries. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<Result> EstablishStepPlanAsync(Guid runId, IReadOnlyList<AnalysisRunStepPlanEntry> plan, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously transitions a run to its next expected active stage.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="expectedCurrentState">The state the run must currently hold.</param>
    /// <param name="nextState">The next durable state.</param>
    /// <param name="atUnixMilliseconds">The transition timestamp.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the current durable state after the attempt.</returns>
    Task<Result<AnalysisRunState>> TransitionStageAsync(
        Guid runId,
        AnalysisRunState expectedCurrentState,
        AnalysisRunState nextState,
        long atUnixMilliseconds,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously begins one planned step from <see cref="AnalysisRunStepState.Pending" />.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="stepId">The stable step identifier. Cannot be <see langword="null" />.</param>
    /// <param name="atUnixMilliseconds">The start timestamp.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<Result> BeginStepAsync(Guid runId, string stepId, long atUnixMilliseconds, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously and conditionally finishes one planned step with its durable outcome from
    ///     <see cref="AnalysisRunStepState.Running" />. Only the call that observes the step still running commits.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="outcome">The step outcome. Cannot be <see langword="null" />.</param>
    /// <param name="atUnixMilliseconds">The finish timestamp.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result is <see langword="true" /> when this call committed the step outcome.</returns>
    Task<Result<bool>> FinishStepAsync(Guid runId, AnalysisRunStepOutcome outcome, long atUnixMilliseconds, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously and durably requests cancellation for a run exactly once.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="atUnixMilliseconds">The request timestamp.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>
    ///     A task whose result contains the current detail, or a failure with <c>analysis.unknownRun</c>.
    /// </returns>
    Task<Result<AnalysisRunDetail>> RequestCancellationAsync(Guid runId, long atUnixMilliseconds, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously and conditionally commits one terminal outcome. Only the first successful call for a run
    ///     commits.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="terminal">The strict terminal summary. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result is <see langword="true" /> when this call committed the terminal state.</returns>
    Task<Result<bool>> CommitTerminalAsync(Guid runId, AnalysisTerminalSummary terminal, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously and conditionally commits <see cref="AnalysisRunState.Cancelled" /> for every pending run
    ///     that already carries a durable cancellation request, without taking or running them.
    /// </summary>
    /// <param name="atUnixMilliseconds">The terminal timestamp.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the number of rows finalized.</returns>
    Task<Result<int>> FinalizeCancelledPendingRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously marks every still-active row as <see cref="AnalysisRunState.Interrupted" /> during processor startup.
    /// </summary>
    /// <remarks>
    ///     Correct only because exactly one engine process owns the local-state database at a time, so every active row
    ///     observed before this process starts processing is an orphan of an earlier process. Call this once, from
    ///     <c>AnalysisProcessorHost.StartAsync</c>, before the protocol host begins reading requests.
    /// </remarks>
    /// <param name="atUnixMilliseconds">The interruption timestamp.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the number of rows interrupted.</returns>
    Task<Result<int>> InterruptActiveRunsAsync(long atUnixMilliseconds, CancellationToken cancellationToken);
}
