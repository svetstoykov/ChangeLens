using ChangeLens.Core.Analysis.Snapshots.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Analysis.Snapshots.Interfaces;

/// <summary>
///     Defines committed-content snapshot capture for one accepted analysis run.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one run and do not need to be thread-safe.
/// </remarks>
public interface ISnapshotCaptureService
{
    /// <summary>Asynchronously captures the committed comparison for one accepted run.</summary>
    /// <param name="request">The accepted run and revision identities for capture. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the manifest and the excluded-uncommitted warning metadata.</returns>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken" /> is canceled.</exception>
    Task<Result<SnapshotCapture>> CaptureAsync(SnapshotCaptureRequest request, CancellationToken cancellationToken);
}
