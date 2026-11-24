using ChangeLens.Engine.AnalysisRuns.Handlers;
using ChangeLens.Engine.Comparisons.Handlers;
using ChangeLens.Engine.EngineStatus.Handlers;
using ChangeLens.Engine.Preferences.Handlers;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Repositories.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChangeLens.Engine.Protocol.Extensions;

/// <summary>
///     Registers every approved protocol action handler on <see cref="HostApplicationBuilder" />.
/// </summary>
internal static class ActionHandlersHostApplicationBuilderExtensions
{
    /// <summary>Registers every approved action handler as a keyed scoped service.</summary>
    /// <param name="builder">The host application builder to configure. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    internal static void AddActionHandlers(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddActionHandler<RepositoryOpenHandler>(builder);
        AddActionHandler<RepositoryRestoreLastHandler>(builder);
        AddActionHandler<RepositoryListRecentHandler>(builder);
        AddActionHandler<RepositoryRemoveRecentHandler>(builder);
        AddActionHandler<ComparisonListTargetsHandler>(builder);
        AddActionHandler<ComparisonPrepareHandler>(builder);
        AddActionHandler<ComparisonCheckFreshnessHandler>(builder);
        AddActionHandler<ComparisonCheckRemoteBaselineHandler>(builder);
        AddActionHandler<ComparisonRefreshRemoteBaselineHandler>(builder);
        AddActionHandler<PreferenceGetColorThemeHandler>(builder);
        AddActionHandler<PreferenceSetColorThemeHandler>(builder);
        AddActionHandler<EngineCheckStatusHandler>(builder);
        AddActionHandler<AnalysisStartHandler>(builder);
        AddActionHandler<AnalysisGetActiveHandler>(builder);
        AddActionHandler<AnalysisPollRunHandler>(builder);
        AddActionHandler<AnalysisCancelHandler>(builder);
    }

    private static void AddActionHandler<THandler>(HostApplicationBuilder builder)
        where THandler : class, IActionHandler =>
        builder.Services.AddKeyedScoped(typeof(IActionHandler), THandler.Action, typeof(THandler));
}
