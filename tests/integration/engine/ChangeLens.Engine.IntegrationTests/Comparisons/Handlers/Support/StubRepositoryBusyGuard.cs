using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.Comparisons.Handlers.Support;

/// <summary>
///     Allows every repository-scoped action in focused comparison-handler tests.
/// </summary>
internal sealed class StubRepositoryBusyGuard : IRepositoryBusyGuard
{
    /// <inheritdoc />
    public Task<Result> CheckPathAsync(string? path, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());

    /// <inheritdoc />
    public Task<Result> CheckRepositoryIdAsync(string? repositoryId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
