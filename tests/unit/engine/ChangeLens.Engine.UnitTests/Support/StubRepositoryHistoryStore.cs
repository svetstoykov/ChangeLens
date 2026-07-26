using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.UnitTests.Support;

internal sealed class StubRepositoryHistoryStore : IRepositoryHistoryStore
{
    private static readonly Guid RepositoryId =
        Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    public Task<Result<RepositoryHistoryEntry>> RecordOpenAsync(
        string canonicalPath,
        string canonicalPathKey,
        string displayName,
        long openedAtUnixMilliseconds,
        CancellationToken cancellationToken) =>
        Task.FromResult(
            Result.Success(
                new RepositoryHistoryEntry(
                    RepositoryId,
                    canonicalPath,
                    displayName,
                    openedAtUnixMilliseconds,
                    null)));

    public Task<Result<RepositoryHistoryEntry?>> GetLastAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success<RepositoryHistoryEntry?>(null));

    public Task<Result<RepositoryHistorySnapshot>> ListRecentAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(
            Result.Success(
                new RepositoryHistorySnapshot(null, Array.Empty<RepositoryHistoryEntry>())));

    public Task<Result> RemoveAsync(Guid repositoryId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());

    public Task<Result> SetPreferredTargetAsync(
        string canonicalPathKey,
        string preferredTargetFullName,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
