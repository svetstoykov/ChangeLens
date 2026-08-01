using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.EngineStatus.Constants;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.Preferences.Constants;
using ChangeLens.Engine.Repositories.Constants;

namespace ChangeLens.Engine.Protocol.Constants;

/// <summary>
///     Provides the exact set of actions approved for engine protocol routing.
/// </summary>
internal static class EngineActionConstants
{
    /// <summary>
    ///     Gets the approved protocol actions in registration order.
    /// </summary>
    internal static readonly IReadOnlyList<string> ApprovedActions =
    [
        RepositoryActionConstants.OpenAction,
        RepositoryActionConstants.RestoreLastAction,
        RepositoryActionConstants.ListRecentAction,
        RepositoryActionConstants.RemoveRecentAction,
        ComparisonActionConstants.ListTargetsAction,
        ComparisonActionConstants.PrepareAction,
        ComparisonActionConstants.CheckFreshnessAction,
        ComparisonActionConstants.CheckRemoteBaselineAction,
        ComparisonActionConstants.RefreshRemoteBaselineAction,
        PreferenceActionConstants.GetColorThemeAction,
        PreferenceActionConstants.SetColorThemeAction,
        EngineStatusActionConstants.CheckStatusAction,
        AnalysisActionConstants.StartAction,
        AnalysisActionConstants.GetActiveAction,
        AnalysisActionConstants.PollRunAction,
        AnalysisActionConstants.CancelAction,
    ];
}
