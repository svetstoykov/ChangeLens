using ChangeLens.Infrastructure.LocalState.Constants;
using ChangeLens.Infrastructure.LocalState.Extensions;
using ChangeLens.Infrastructure.LocalState.Models;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Hosting.Extensions;

/// <summary>
///     Registers local-state persistence services on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class LocalStateHostApplicationBuilderExtensions
{
    /// <summary>Registers local-state persistence services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddLocalStateServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var paths = LocalStatePaths.Resolve(builder.Configuration[LocalStateConstants.DirectoryConfigurationKey]);
        builder.Services.AddLocalStateInfrastructure(paths);
    }
}
