namespace ChangeLens.Core.Repositories.Constants;

/// <summary>
///     Provides stable error codes for repository operations.
/// </summary>
public static class RepositoryErrorCode
{
    /// <summary>
    ///     The selected repository path is invalid.
    /// </summary>
    public const string InvalidPath = "repository.invalidPath";

    /// <summary>
    ///     The selected repository path does not identify an existing directory.
    /// </summary>
    public const string PathNotFound = "repository.pathNotFound";

    /// <summary>
    ///     Access to the selected repository path was denied.
    /// </summary>
    public const string AccessDenied = "repository.accessDenied";

    /// <summary>
    ///     The selected path is not inside a Git repository.
    /// </summary>
    public const string NotGitRepository = "repository.notGitRepository";

    /// <summary>
    ///     The selected repository does not provide a working tree.
    /// </summary>
    public const string WorkTreeUnavailable = "repository.workTreeUnavailable";

    /// <summary>
    ///     The selected repository does not provide a committed HEAD revision.
    /// </summary>
    public const string HeadUnavailable = "repository.headUnavailable";

    /// <summary>
    ///     Repository inspection failed without a more specific known reason.
    /// </summary>
    public const string InspectionFailed = "repository.inspectionFailed";
}
