namespace ChangeLens.Engine.Comparisons.Constants;

/// <summary>
///     Provides fixed comparison action names and protocol-envelope limits.
/// </summary>
internal static class ComparisonActionConstants
{
    /// <summary>
    ///     Defines the action that lists comparison targets.
    /// </summary>
    internal const string ListTargetsAction = "comparisons.listTargets";

    /// <summary>
    ///     Defines the action that prepares comparison facts.
    /// </summary>
    internal const string PrepareAction = "comparisons.prepare";

    /// <summary>
    ///     Defines the action that checks prepared comparison freshness.
    /// </summary>
    internal const string CheckFreshnessAction = "comparisons.checkFreshness";

    /// <summary>
    ///     Defines the maximum complete UTF-8 comparison-target response size.
    /// </summary>
    internal const int TargetPageBudgetBytes = 48 * 1024;
}
