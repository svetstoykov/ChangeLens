using ChangeLens.Engine.Logging.Extensions;
using ChangeLens.Engine.Protocol.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Hosting.Extensions;

/// <summary>
///     Provides engine-specific composition for the Generic Host builder.
/// </summary>
internal static class EngineHostApplicationBuilderExtensions
{
    /// <summary>
    ///     Adds the engine protocol boundary and its supporting services to the host builder.
    /// </summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="builder" /> is <see langword="null" />.
    /// </exception>
    internal static void AddEngine(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddEngineLogging();
        builder.Services.AddSingleton<TextReader>(_ => Console.In);
        builder.Services.AddSingleton<TextWriter>(_ => Console.Out);
        builder.Services.AddHostedService<EngineProtocolService>();
    }
}
