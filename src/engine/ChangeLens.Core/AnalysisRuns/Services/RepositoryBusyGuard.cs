using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.AnalysisRuns.Services;

/// <summary>
///     Implements the repository-busy check against the durable active-run projection.
/// </summary>
/// <remarks>
///     The host registers this implementation as scoped. It serves one request and does not need to be thread-safe.
/// </remarks>
/// <param name="analysisRunStore">The scoped analysis run store. Cannot be <see langword="null" />.</param>
/// <param name="repositoryHistoryStore">The scoped repository history store. Cannot be <see langword="null" />.</param>
/// <param name="pathResolver">The scoped repository path resolver. Cannot be <see langword="null" />.</param>
/// <param name="pathKeyProvider">The scoped canonical repository path-key provider. Cannot be <see langword="null" />.</param>
public sealed class RepositoryBusyGuard(
    IAnalysisRunStore analysisRunStore,
    IRepositoryHistoryStore repositoryHistoryStore,
    IRepositoryPathResolver pathResolver,
    ICanonicalRepositoryPathKeyProvider pathKeyProvider) : IRepositoryBusyGuard
{
    /// <inheritdoc />
    public async Task<Result> CheckPathAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Success();
        }

        var resolution = await pathResolver.ResolveAsync(path, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result.Success();
        }

        return await this.CheckCanonicalPathKeyAsync(pathKeyProvider.CreateKey(resolution.Data!), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> CheckRepositoryIdAsync(string? repositoryId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(repositoryId, "D", out var parsedRepositoryId) ||
            !string.Equals(repositoryId, parsedRepositoryId.ToString("D"), StringComparison.Ordinal))
        {
            return Result.Success();
        }

        var lookup = await repositoryHistoryStore.ListRecentAsync(cancellationToken);
        if (lookup.IsFailure)
        {
            return Result.ErrorFromResult(lookup);
        }

        var historyEntry = lookup.Data!.Repositories.SingleOrDefault(entry => entry.RepositoryId == parsedRepositoryId);
        if (historyEntry is null)
        {
            return Result.Success();
        }

        return await this.CheckCanonicalPathKeyAsync(pathKeyProvider.CreateKey(historyEntry.CanonicalPath), cancellationToken);
    }

    private async Task<Result> CheckCanonicalPathKeyAsync(string canonicalPathKey, CancellationToken cancellationToken)
    {
        var activeResult = await analysisRunStore.GetActiveByRepositoryAsync(canonicalPathKey, cancellationToken);
        if (activeResult.IsFailure)
        {
            return Result.ErrorFromResult(activeResult);
        }

        return activeResult.Data is null
            ? Result.Success()
            : Result.Fail(OperationError.Conflict("An active analysis run reserves this repository.", RepositoryErrorCode.Busy));
    }
}
