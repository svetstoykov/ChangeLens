using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Returns a controlled recent-repository result for focused reservation-guard tests.
/// </summary>
/// <param name="listResult">The result returned when recent repositories are listed. Cannot be <see langword="null" />.</param>
internal sealed class StubRepositoryHistoryStore(Result<RepositoryHistorySnapshot> listResult) : IRepositoryHistoryStore
{
    /// <inheritdoc />
    public Task<Result<RepositoryHistoryEntry>> RecordOpenAsync(
        string canonicalPath,
        string canonicalPathKey,
        string displayName,
        long openedAtUnixMilliseconds,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<RepositoryHistoryEntry?>> GetLastAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(listResult);

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid repositoryId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result> SetPreferredTargetAsync(
        string canonicalPathKey,
        string preferredTargetFullName,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
