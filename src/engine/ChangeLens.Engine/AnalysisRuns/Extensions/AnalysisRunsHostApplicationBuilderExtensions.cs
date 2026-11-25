using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Services;
using ChangeLens.Engine.AnalysisRuns.Hosting;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using ChangeLens.Engine.AnalysisRuns.Services;
using ChangeLens.Infrastructure.AnalysisRuns.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.AnalysisRuns.Extensions;

/// <summary>
///     Registers durable analysis-run coordination, busy-guard, and processor hosting services on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class AnalysisRunsHostApplicationBuilderExtensions
{
    /// <summary>Registers analysis run lifecycle services and the background processor host.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddAnalysisRunServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IAnalysisProcessorControl, AnalysisProcessorControl>();
        builder.Services.AddAnalysisRunInfrastructure();
        builder.Services.AddScoped<IAnalysisRunCoordinator, AnalysisRunCoordinator>();
        builder.Services.AddScoped<IRepositoryBusyGuard, RepositoryBusyGuard>();
        builder.Services.AddHostedService<AnalysisProcessorHost>();
    }
}
