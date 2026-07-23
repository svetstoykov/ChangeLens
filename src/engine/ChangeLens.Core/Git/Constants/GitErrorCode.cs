namespace ChangeLens.Core.Git.Constants;

/// <summary>
///     Provides stable error codes for Git execution failures.
/// </summary>
public static class GitErrorCode
{
    /// <summary>
    ///     The installed Git executable is unavailable or cannot run.
    /// </summary>
    public const string Unavailable = "git.unavailable";

    /// <summary>
    ///     Git repository inspection exceeded its allowed time.
    /// </summary>
    public const string TimedOut = "git.timedOut";
}
