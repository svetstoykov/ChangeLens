using ChangeLens.Infrastructure.LocalState.Persistence;

namespace ChangeLens.Infrastructure.LocalState.Interfaces;

/// <summary>
///     Defines controlled Entity Framework contexts for the local-state database.
/// </summary>
public interface ILocalStateDatabase
{
    /// <summary>
    ///     Asynchronously creates one configured context with required foreign-key enforcement.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the configured context.</returns>
    Task<ChangeLensLocalStateDbContext> CreateContextAsync(CancellationToken cancellationToken);
}
