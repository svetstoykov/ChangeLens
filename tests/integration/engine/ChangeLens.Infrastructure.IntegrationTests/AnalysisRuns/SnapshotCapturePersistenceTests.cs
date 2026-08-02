using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns.Support;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.AnalysisRuns;

/// <summary>
///     Verifies that the guarded capture transaction commits the run header and every manifest entry together.
/// </summary>
public sealed class SnapshotCapturePersistenceTests
{
    /// <summary>
    ///     Asynchronously commits the capture header and entries in one transaction.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CommitCaptureAsyncWritesHeaderAndEntriesTogether()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateCapturingRunAsync();
        var capture = fixture.Capture(runId, entryCount: 3, excludedTotal: 2);

        var committed = await fixture.Store.CommitCaptureAsync(runId, capture, 2_000, TestContext.Current.CancellationToken);

        Assert.True(committed.IsSuccess);
        Assert.True(committed.Data);
        var run = await fixture.GetRunAsync(runId);
        Assert.Equal(2_000, run.CapturedAtUnixMilliseconds);
        Assert.Equal(capture.Manifest.SnapshotId, run.SnapshotId);
        Assert.Equal(capture.Manifest.ManifestHash, run.ManifestHash);
        Assert.Equal(capture.Manifest.MergeBaseRevision, run.MergeBaseRevision);
        Assert.Equal(3, run.CapturedChangedFileCount);
        Assert.Equal(2, run.ExcludedUncommittedTotal);
        Assert.Equal(3, (await fixture.GetManifestEntriesAsync(runId)).Count);
    }

    /// <summary>
    ///     Asynchronously refuses to commit and writes no entries once cancellation is durably recorded.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CommitCaptureAsyncDurableCancellationWinsAndWritesNothing()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateCapturingRunAsync();
        await fixture.Store.RequestCancellationAsync(runId, 1_500, TestContext.Current.CancellationToken);
        var capture = fixture.Capture(runId, entryCount: 2, excludedTotal: 0);

        var committed = await fixture.Store.CommitCaptureAsync(runId, capture, 2_000, TestContext.Current.CancellationToken);

        Assert.True(committed.IsSuccess);
        Assert.False(committed.Data);
        var run = await fixture.GetRunAsync(runId);
        Assert.Null(run.CapturedAtUnixMilliseconds);
        Assert.Null(run.SnapshotId);
        Assert.Empty(await fixture.GetManifestEntriesAsync(runId));
    }

    /// <summary>
    ///     Asynchronously refuses a second commit and never double-writes manifest rows.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CommitCaptureAsyncRetryDoesNotDoubleWrite()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateCapturingRunAsync();
        var capture = fixture.Capture(runId, entryCount: 2, excludedTotal: 0);

        var first = await fixture.Store.CommitCaptureAsync(runId, capture, 2_000, TestContext.Current.CancellationToken);
        var second = await fixture.Store.CommitCaptureAsync(runId, capture, 2_100, TestContext.Current.CancellationToken);

        Assert.True(first.Data);
        Assert.False(second.Data);
        Assert.Equal(2, (await fixture.GetManifestEntriesAsync(runId)).Count);
        Assert.Equal(2_000, (await fixture.GetRunAsync(runId)).CapturedAtUnixMilliseconds);
    }

    /// <summary>
    ///     Asynchronously rejects a manifest whose header does not match the accepted run identity.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CommitCaptureAsyncMismatchedIdentityIsRejectedWithoutWriting()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateCapturingRunAsync();
        var capture = fixture.Capture(runId, entryCount: 1, excludedTotal: 0);
        var mismatched = capture with { Manifest = capture.Manifest with { HeadRevision = new string('b', 40) } };

        var committed = await fixture.Store.CommitCaptureAsync(runId, mismatched, 2_000, TestContext.Current.CancellationToken);

        Assert.True(committed.IsFailure);
        Assert.Null((await fixture.GetRunAsync(runId)).CapturedAtUnixMilliseconds);
        Assert.Empty(await fixture.GetManifestEntriesAsync(runId));
    }

    /// <summary>
    ///     Asynchronously reads the persisted capture back through the run detail.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task GetDetailAsyncSurfacesThePersistedCapture()
    {
        await using var fixture = await AnalysisRunStoreTestFixture.CreateAsync();
        var runId = await fixture.CreateCapturingRunAsync();
        var capture = fixture.Capture(runId, entryCount: 4, excludedTotal: 3);
        await fixture.Store.CommitCaptureAsync(runId, capture, 2_000, TestContext.Current.CancellationToken);

        var detail = await fixture.Store.GetDetailAsync(runId, TestContext.Current.CancellationToken);

        Assert.True(detail.IsSuccess);
        Assert.Equal(capture.Manifest.SnapshotId, detail.Data!.SnapshotId);
        Assert.Equal(2_000, detail.Data.CapturedAtUnixMilliseconds);
        Assert.Equal(capture.Manifest.ManifestHash, detail.Data.ManifestHash);
        Assert.Equal(4, detail.Data.CapturedChangedFileCount);
        Assert.Equal(3, detail.Data.ExcludedUncommittedCounts!.Total);
    }
}
