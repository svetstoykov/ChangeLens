using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using ChangeLens.Infrastructure.LocalState.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Infrastructure.LocalState.Extensions;

/// <summary>
///     Registers SQLite local-state persistence and initialization services.
/// </summary>
public static class LocalStateServiceCollectionExtensions
{
    /// <summary>
    ///     Adds local-state paths, the SQLite database context, and the scoped initializer.
    /// </summary>
    /// <param name="services">The service collection to configure. Cannot be <see langword="null" />.</param>
    /// <param name="paths">The resolved local-state filesystem paths. Cannot be <see langword="null" />.</param>
    /// <returns>The supplied service collection.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="services" /> is <see langword="null" />.
    ///     -or-
    ///     <paramref name="paths" /> is <see langword="null" />.
    /// </exception>
    public static IServiceCollection AddLocalStateInfrastructure(this IServiceCollection services, LocalStatePaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = LocalStateConstants.CommandTimeoutSeconds,
            ForeignKeys = true,
        }.ToString();

        services.AddSingleton(paths);
        services.AddDbContext<ChangeLensLocalStateDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ILocalStateInitializer, SqliteLocalStateInitializer>();
        return services;
    }
}
