using ChangeLens.Core.Preferences.Interfaces;
using ChangeLens.Infrastructure.Preferences.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Infrastructure.Preferences.Extensions;

/// <summary>
///     Registers SQLite-backed preference persistence services.
/// </summary>
public static class PreferenceServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the scoped color-theme preference store.
    /// </summary>
    /// <param name="services">The service collection to configure. Cannot be <see langword="null" />.</param>
    /// <returns>The supplied service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddPreferenceInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IColorThemePreferenceStore, SqliteColorThemePreferenceStore>();
        return services;
    }
}
