using System.Diagnostics;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Helpers;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.EngineStatus.Services;

/// <summary>
///     Provides the engine readiness check through the local-state database.
/// </summary>
/// <remarks>
///     The Engine registers this implementation as scoped. It uses the request context and does not need to be
///     thread-safe.
/// </remarks>
/// <param name="context">The scoped local-state context. Cannot be <see langword="null" />.</param>
/// <param name="logger">The readiness check logger. Cannot be <see langword="null" />.</param>
public sealed class EngineStatusService(
    ChangeLensLocalStateDbContext context,
    ILogger<EngineStatusService> logger) : IEngineStatusService
{
    /// <inheritdoc />
    public async Task<Result> CheckStatusAsync(CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var canConnect = await context.Database.CanConnectAsync(cancellationToken);
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        logger.LogDebug("Checked SQLite local-state connectivity; result was {CanConnect}.", canConnect);
        if (canConnect)
        {
            logger.LogInformation("Engine readiness check succeeded in {ElapsedMilliseconds:0.000} ms.", elapsedMilliseconds);
            return Result.Success();
        }

        logger.LogInformation(
            "Engine readiness check failed with error {ErrorCode} in {ElapsedMilliseconds:0.000} ms.",
            LocalStateErrorCode.Unavailable, elapsedMilliseconds);
        return LocalStateFailure.Unavailable();
    }
}
