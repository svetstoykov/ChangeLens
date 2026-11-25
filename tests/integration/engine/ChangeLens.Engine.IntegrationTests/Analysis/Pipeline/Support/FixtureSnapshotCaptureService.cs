using ChangeLens.Core.Analysis.Snapshots.Interfaces;
using ChangeLens.Core.Analysis.Snapshots.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Pipeline.Support;

/// <summary>Provides a valid committed snapshot for production-host analysis fixture tests.</summary>
internal sealed class FixtureSnapshotCaptureService : ISnapshotCaptureService
{
    /// <inheritdoc />
    public Task<Result<SnapshotCapture>> CaptureAsync(SnapshotCaptureRequest request, CancellationToken cancellationToken)
    {
        var entry = new SnapshotManifestEntry("captured.txt", null, SnapshotChangeCategory.Modified, "100644", "100644",
            new string('a', 40), new string('b', 40));
        var manifest = new SnapshotManifest(Guid.NewGuid(), new string('c', 64), request.CanonicalRepositoryPathKey,
            request.Target, request.TargetRevision, request.HeadRevision, new string('d', 40), [entry]);
        return Task.FromResult(Result.Success(new SnapshotCapture(manifest, new ExcludedUncommittedCounts(0, 0, 0, 0, 0))));
    }
}
