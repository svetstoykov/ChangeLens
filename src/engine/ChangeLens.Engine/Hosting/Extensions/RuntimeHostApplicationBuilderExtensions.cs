using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Hosting.Extensions;

/// <summary>
///     Registers process-owned runtime services on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class RuntimeHostApplicationBuilderExtensions
{
    /// <summary>Registers process-owned runtime services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddRuntimeServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<TextReader>(_ => Console.In);
        builder.Services.AddSingleton<TextWriter>(_ => Console.Out);
        builder.Services.AddSingleton(TimeProvider.System);
    }
}
