using ChangeLens.Core.Preferences.Interfaces;
using ChangeLens.Core.Preferences.Services;
using ChangeLens.Infrastructure.Preferences.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Preferences.Extensions;

/// <summary>
///     Registers color-theme preference services on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class PreferencesHostApplicationBuilderExtensions
{
    /// <summary>Registers color-theme preference services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddPreferenceServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPreferenceInfrastructure();
        builder.Services.AddScoped<IColorThemePreferenceService, ColorThemePreferenceService>();
    }
}
