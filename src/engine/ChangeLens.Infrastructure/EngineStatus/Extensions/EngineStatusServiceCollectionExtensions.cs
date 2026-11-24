using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Infrastructure.EngineStatus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Infrastructure.EngineStatus.Extensions;

/// <summary>
///     Registers engine readiness check infrastructure services.
/// </summary>
public static class EngineStatusServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the scoped engine status service.
    /// </summary>
    /// <param name="services">The service collection to configure. Cannot be <see langword="null" />.</param>
    /// <returns>The supplied service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddEngineStatusInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IEngineStatusService, EngineStatusService>();
        return services;
    }
}
