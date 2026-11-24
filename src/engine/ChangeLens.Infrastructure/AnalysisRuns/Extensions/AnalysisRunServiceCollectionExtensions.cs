using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Infrastructure.AnalysisRuns.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Infrastructure.AnalysisRuns.Extensions;

/// <summary>
///     Registers SQLite-backed analysis-run persistence services.
/// </summary>
public static class AnalysisRunServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the scoped analysis-run store.
    /// </summary>
    /// <param name="services">The service collection to configure. Cannot be <see langword="null" />.</param>
    /// <returns>The supplied service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddAnalysisRunInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAnalysisRunStore, SqliteAnalysisRunStore>();
        return services;
    }
}
