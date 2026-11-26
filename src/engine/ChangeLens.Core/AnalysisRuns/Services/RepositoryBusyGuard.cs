using System.Diagnostics;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Repositories.Interfaces;
using ChangeLens.Core.Results.Models;
using Microsoft.Extensions.Logging;

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
/// <param name="logger">The logger for repository-busy outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">Any injected dependency is <see langword="null" />.</exception>
public sealed class RepositoryBusyGuard(
    IAnalysisRunStore analysisRunStore,
    IRepositoryHistoryStore repositoryHistoryStore,
    IRepositoryPathResolver pathResolver,
    ICanonicalRepositoryPathKeyProvider pathKeyProvider,
    ILogger<RepositoryBusyGuard> logger) : IRepositoryBusyGuard
{
    private readonly IAnalysisRunStore _analysisRunStore = analysisRunStore ?? throw new ArgumentNullException(nameof(analysisRunStore));
    private readonly IRepositoryHistoryStore _repositoryHistoryStore =
        repositoryHistoryStore ?? throw new ArgumentNullException(nameof(repositoryHistoryStore));
    private readonly IRepositoryPathResolver _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
    private readonly ICanonicalRepositoryPathKeyProvider _pathKeyProvider =
        pathKeyProvider ?? throw new ArgumentNullException(nameof(pathKeyProvider));
    private readonly ILogger<RepositoryBusyGuard> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<Result> CheckPathAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Success();
        }

        var resolution = await this._pathResolver.ResolveAsync(path, cancellationToken);
        if (resolution.IsFailure)
        {
            this._logger.LogDebug("Repository busy check by path skipped because path resolution did not succeed.");
            return Result.Success();
        }

        return await this.CheckCanonicalPathKeyAsync(this._pathKeyProvider.CreateKey(resolution.Data!), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> CheckRepositoryIdAsync(string? repositoryId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(repositoryId, "D", out var parsedRepositoryId) ||
            !string.Equals(repositoryId, parsedRepositoryId.ToString("D"), StringComparison.Ordinal))
        {
            return Result.Success();
        }

        var lookup = await this._repositoryHistoryStore.ListRecentAsync(cancellationToken);
        if (lookup.IsFailure)
        {
            return Result.ErrorFromResult(lookup);
        }

        var historyEntry = lookup.Data!.Repositories.SingleOrDefault(entry => entry.RepositoryId == parsedRepositoryId);
        if (historyEntry is null)
        {
            this._logger.LogDebug(
                "Repository busy check found no recent history entry for repository {RepositoryId}.", parsedRepositoryId);
            return Result.Success();
        }

        return await this.CheckCanonicalPathKeyAsync(this._pathKeyProvider.CreateKey(historyEntry.CanonicalPath), cancellationToken);
    }

    private async Task<Result> CheckCanonicalPathKeyAsync(string canonicalPathKey, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var activeResult = await this._analysisRunStore.GetActiveByRepositoryAsync(canonicalPathKey, cancellationToken);
        if (activeResult.IsFailure)
        {
            return Result.ErrorFromResult(activeResult);
        }

        if (activeResult.Data is not null)
        {
            this._logger.LogWarning(
                "Repository busy check rejected request because active run {RunId} reserves the repository ({ErrorCode}) in " +
                "{ElapsedMilliseconds:0.000} ms.",
                activeResult.Data.RunId, RepositoryErrorCode.Busy, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Fail(OperationError.Conflict("An active analysis run reserves this repository.", RepositoryErrorCode.Busy));
        }

        this._logger.LogDebug(
            "Repository busy check confirmed no active run reserves the repository in {ElapsedMilliseconds:0.000} ms.",
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        return Result.Success();
    }
}
