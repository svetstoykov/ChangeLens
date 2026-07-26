using Microsoft.Data.Sqlite;

namespace ChangeLens.Infrastructure.LocalState.Interfaces;

/// <summary>
///     Defines controlled connections to the local-state database.
/// </summary>
public interface ILocalStateDatabase
{
    /// <summary>
    ///     Asynchronously opens one configured connection with required foreign-key enforcement.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the open connection.</returns>
    Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
