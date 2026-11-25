using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Engine.Comparisons.Interfaces;
using ChangeLens.Engine.Comparisons.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Comparisons.Extensions;

/// <summary>
///     Registers comparison services on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class ComparisonsHostApplicationBuilderExtensions
{
    /// <summary>Registers comparison services.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddComparisonServices(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<IComparisonFileSummaryComposer, ComparisonFileSummaryComposer>();
        builder.Services.AddScoped<IGitComparisonTargetDiscovery, GitComparisonTargetDiscovery>();
        builder.Services.AddScoped<IGitComparisonPreparer, GitComparisonPreparer>();
        builder.Services.AddScoped<IGitComparisonFreshnessChecker, GitComparisonFreshnessChecker>();
        builder.Services.AddScoped<IGitRemoteBaselineTracker, GitRemoteBaselineTracker>();
        builder.Services.AddScoped<IComparisonTargetPageBuilder, ComparisonTargetPageBuilder>();
    }
}
