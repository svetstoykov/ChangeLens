using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.LocalState.Services;

namespace ChangeLens.Infrastructure.EngineStatus.Services;

/// <summary>
///     Provides the engine readiness check through the local-state database.
/// </summary>
/// <remarks>
///     The Engine registers this implementation as scoped. It uses the request context and does not need to be
///     thread-safe.
/// </remarks>
/// <param name="context">The scoped local-state context. Cannot be <see langword="null" />.</param>
public sealed class EngineStatusService(ChangeLensLocalStateDbContext context) : IEngineStatusService
{
    /// <inheritdoc />
    public async Task<Result> CheckStatusAsync(CancellationToken cancellationToken) =>
        await context.Database.CanConnectAsync(cancellationToken)
            ? Result.Success()
            : LocalStateFailure.Unavailable();
}
