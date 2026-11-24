using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Repositories.Interfaces;
using ChangeLens.Core.Repositories.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Infrastructure.Git.Extensions;
using ChangeLens.Infrastructure.Git.Models;
using ChangeLens.Infrastructure.Repositories.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Repositories.Extensions;

/// <summary>
///     Registers repository inspection and history services on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class RepositoriesHostApplicationBuilderExtensions
{
    /// <summary>Registers repository inspection and history services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddRepositoryServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRepositoryInfrastructure();
        builder.Services.AddGitInfrastructure(
            new GitCommandRunnerOptions
            {
                ExecutablePath = builder.Configuration[RepositoryInspectionConfigurationConstants.GitExecutableConfigurationKey],
            });
        builder.Services.AddScoped<IRepositoryHistoryService, RepositoryHistoryService>();
        builder.Services.AddScoped<IGitRepositoryInspector, GitRepositoryInspector>();
    }
}
