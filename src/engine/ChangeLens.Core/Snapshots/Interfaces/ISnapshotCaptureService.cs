using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Core.Snapshots.Interfaces;

/// <summary>
///     Defines committed-content snapshot capture for one accepted analysis run.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one run and do not need to be thread-safe.
/// </remarks>
public interface ISnapshotCaptureService
{
    /// <summary>Asynchronously captures the committed comparison for one accepted run.</summary>
    /// <param name="run">The durable run detail read at capture time. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the manifest and the excluded-uncommitted warning metadata.</returns>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken" /> is canceled.</exception>
    Task<Result<SnapshotCapture>> CaptureAsync(AnalysisRunDetail run, CancellationToken cancellationToken);
}
