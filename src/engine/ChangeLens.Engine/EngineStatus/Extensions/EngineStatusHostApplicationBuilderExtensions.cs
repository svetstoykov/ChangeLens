using ChangeLens.Infrastructure.EngineStatus.Extensions;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.EngineStatus.Extensions;

/// <summary>
///     Registers engine readiness services on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class EngineStatusHostApplicationBuilderExtensions
{
    /// <summary>Registers engine readiness services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddEngineStatusServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddEngineStatusInfrastructure();
    }
}
