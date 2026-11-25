using ChangeLens.Core.Repositories.Interfaces;
using ChangeLens.Infrastructure.Repositories.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Infrastructure.Repositories.Extensions;

/// <summary>
///     Registers filesystem and SQLite repository infrastructure services.
/// </summary>
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    ///     Adds scoped repository path resolution, canonical key generation, and history persistence services.
    /// </summary>
    /// <param name="services">The service collection to configure. Cannot be <see langword="null" />.</param>
    /// <returns>The supplied service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddRepositoryInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICanonicalRepositoryPathKeyProvider, CanonicalRepositoryPathKeyProvider>();
        services.AddScoped<IRepositoryPathResolver, PhysicalRepositoryPathResolver>();
        services.AddScoped<IRepositoryHistoryStore, SqliteRepositoryHistoryStore>();
        return services;
    }
}
