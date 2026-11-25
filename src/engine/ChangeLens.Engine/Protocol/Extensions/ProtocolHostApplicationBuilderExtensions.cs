using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Protocol.Extensions;

/// <summary>
///     Registers singleton protocol transport services and the protocol host on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class ProtocolHostApplicationBuilderExtensions
{
    /// <summary>Registers singleton protocol transport services and the protocol host.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddProtocolServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IEngineProtocolSerializer, EngineProtocolSerializer>();
        builder.Services.AddSingleton<IEngineProtocolTransport, EngineProtocolTransport>();
        builder.Services.AddHostedService<EngineProtocolHost>();
    }
}
