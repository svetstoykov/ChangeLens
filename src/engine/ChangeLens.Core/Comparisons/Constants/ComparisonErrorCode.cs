namespace ChangeLens.Core.Comparisons.Constants;

/// <summary>
///     Provides stable error codes for comparison operations.
/// </summary>
public static class ComparisonErrorCode
{
    /// <summary>
    ///     Identifies a target query that violates comparison validation rules.
    /// </summary>
    public const string InvalidTargetQuery = "comparison.invalidTargetQuery";

    /// <summary>
    ///     Identifies an invalid target-page continuation request.
    /// </summary>
    public const string InvalidTargetPage = "comparison.invalidTargetPage";

    /// <summary>
    ///     Identifies a target set that changed during paging or preparation.
    /// </summary>
    public const string TargetsChanged = "comparison.targetsChanged";

    /// <summary>
    ///     Identifies a selected target that is no longer available.
    /// </summary>
    public const string TargetUnavailable = "comparison.targetUnavailable";

    /// <summary>
    ///     Identifies a selected target that is not supported.
    /// </summary>
    public const string TargetInvalid = "comparison.targetInvalid";

    /// <summary>
    ///     Identifies a selected target that shares no history with the current revision.
    /// </summary>
    public const string UnrelatedHistory = "comparison.unrelatedHistory";

    /// <summary>
    ///     Identifies revisions with multiple best merge bases.
    /// </summary>
    public const string AmbiguousBase = "comparison.ambiguousBase";

    /// <summary>
    ///     Identifies repository facts that changed during comparison preparation.
    /// </summary>
    public const string ChangedDuringPreparation = "comparison.changedDuringPreparation";

    /// <summary>
    ///     Identifies comparison facts that exceed the supported local inspection limit.
    /// </summary>
    public const string TooLarge = "comparison.tooLarge";

    /// <summary>
    ///     Identifies comparison inspection that exceeded its allowed time.
    /// </summary>
    public const string TimedOut = "comparison.timedOut";

    /// <summary>
    ///     Identifies malformed or unavailable Git comparison inspection.
    /// </summary>
    public const string InspectionFailed = "comparison.inspectionFailed";

    /// <summary>
    ///     Identifies an invalid comparison freshness token.
    /// </summary>
    public const string InvalidFreshnessToken = "comparison.invalidFreshnessToken";

    /// <summary>
    ///     Identifies a remote that could not be reached.
    /// </summary>
    public const string RemoteUnreachable = "comparison.remoteUnreachable";

    /// <summary>
    ///     Identifies a remote that requires interactive authentication ChangeLens does not perform.
    /// </summary>
    public const string RemoteAuthenticationRequired = "comparison.remoteAuthenticationRequired";

    /// <summary>
    ///     Identifies a remote operation that exceeded its allowed time.
    /// </summary>
    public const string RemoteTimedOut = "comparison.remoteTimedOut";

    /// <summary>
    ///     Identifies a repository with no configured remote for the selected target.
    /// </summary>
    public const string NoRemoteConfigured = "comparison.noRemoteConfigured";
}
