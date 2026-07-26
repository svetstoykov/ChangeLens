namespace ChangeLens.Engine.Repositories.Constants;

/// <summary>
///     Provides stable protocol action names for repository operations.
/// </summary>
internal static class RepositoryActionConstants
{
    /// <summary>
    ///     The action that inspects and opens a selected repository.
    /// </summary>
    internal const string OpenAction = "repositories.open";

    /// <summary>
    ///     The action that restores the last selected repository.
    /// </summary>
    internal const string RestoreLastAction = "repositories.restoreLast";

    /// <summary>
    ///     The action that lists recent repository metadata.
    /// </summary>
    internal const string ListRecentAction = "repositories.listRecent";

    /// <summary>
    ///     The action that removes recent repository metadata.
    /// </summary>
    internal const string RemoveRecentAction = "repositories.removeRecent";
}
