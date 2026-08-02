using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.AnalysisRuns.Interfaces;

/// <summary>
///     Defines the four analysis-run use cases exposed to protocol action handlers.
/// </summary>
internal interface IAnalysisRunCoordinator
{
    /// <summary>Asynchronously starts an analysis run for a prepared comparison.</summary>
    /// <param name="path">The repository path, or <see langword="null" /> when unavailable.</param>
    /// <param name="target">The comparison target, or <see langword="null" /> when unavailable.</param>
    /// <param name="freshnessToken">The comparison freshness token, or <see langword="null" /> when unavailable.</param>
    /// <param name="changeContext">Optional developer-supplied context for the run.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task whose result contains the accepted or rejected start outcome.</returns>
    Task<Result<AnalysisStartOutcome>> StartAsync(
        string? path,
        string? target,
        string? freshnessToken,
        string? changeContext,
        CancellationToken cancellationToken);

    /// <summary>Asynchronously looks up the active analysis run for a repository.</summary>
    /// <param name="path">The repository path, or <see langword="null" /> when unavailable.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task whose result contains the active run, or <see langword="null" /> when none exists.</returns>
    Task<Result<AnalysisRunDetail?>> GetActiveAsync(string? path, CancellationToken cancellationToken);

    /// <summary>Asynchronously reads the current detail of one analysis run.</summary>
    /// <param name="runId">The identifier of the run to read.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task whose result contains the run detail.</returns>
    Task<Result<AnalysisRunDetail>> PollRunAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Asynchronously requests cancellation of one analysis run.</summary>
    /// <param name="runId">The identifier of the run to cancel.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task whose result indicates whether the request was accepted.</returns>
    Task<Result> CancelAsync(Guid runId, CancellationToken cancellationToken);
}
