using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Infrastructure.Git.Models;
using ChangeLens.Infrastructure.Git.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Infrastructure.Git.Extensions;

/// <summary>
///     Registers Git CLI execution infrastructure services.
/// </summary>
public static class GitServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the scoped Git CLI command runner and its text and binary execution ports.
    /// </summary>
    /// <param name="services">The service collection to configure. Cannot be <see langword="null" />.</param>
    /// <param name="options">
    ///     Optional Git runner settings, or <see langword="null" /> to keep existing or default configuration.
    /// </param>
    /// <returns>The supplied service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddGitInfrastructure(this IServiceCollection services, GitCommandRunnerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<GitCommandRunnerOptions>();
        if (options is not null)
        {
            services.Configure<GitCommandRunnerOptions>(configured => configured.ExecutablePath = options.ExecutablePath);
        }

        services.AddScoped<GitCliCommandRunner>();
        services.AddScoped<IGitCommandRunner>(serviceProvider => serviceProvider.GetRequiredService<GitCliCommandRunner>());
        services.AddScoped<IGitBinaryCommandRunner>(serviceProvider => serviceProvider.GetRequiredService<GitCliCommandRunner>());
        return services;
    }
}
