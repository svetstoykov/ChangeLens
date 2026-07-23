namespace ChangeLens.Infrastructure.Git.Constants;

/// <summary>
///     Provides stable process and environment values for controlled Git execution.
/// </summary>
internal static class GitProcessConstants
{
    /// <summary>
    ///     The maximum time allowed for best-effort process and stream cleanup.
    /// </summary>
    internal static readonly TimeSpan CleanupGracePeriod = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     The default Git executable resolved from the process search path.
    /// </summary>
    internal const string DefaultExecutable = "git";

    /// <summary>
    ///     The environment value that disables an optional Git behavior.
    /// </summary>
    internal const string DisabledEnvironmentValue = "0";

    /// <summary>
    ///     The Git Credential Manager value that disables interactive operation.
    /// </summary>
    internal const string NonInteractiveCredentialValue = "Never";

    /// <summary>
    ///     The pager value that forwards output without interactive paging.
    /// </summary>
    internal const string NonInteractivePagerValue = "cat";

    /// <summary>
    ///     The locale value that produces reviewed invariant Git diagnostics.
    /// </summary>
    internal const string InvariantLocaleValue = "C";

    /// <summary>
    ///     The environment variable that disables Git optional locks for read-only operations.
    /// </summary>
    internal const string OptionalLocksEnvironmentVariable = "GIT_OPTIONAL_LOCKS";

    /// <summary>
    ///     The environment variable that controls Git terminal prompts.
    /// </summary>
    internal const string TerminalPromptEnvironmentVariable = "GIT_TERMINAL_PROMPT";

    /// <summary>
    ///     The environment variable that controls Git Credential Manager interaction.
    /// </summary>
    internal const string CredentialInteractionEnvironmentVariable = "GCM_INTERACTIVE";

    /// <summary>
    ///     The Git-specific pager environment variable.
    /// </summary>
    internal const string GitPagerEnvironmentVariable = "GIT_PAGER";

    /// <summary>
    ///     The general pager environment variable.
    /// </summary>
    internal const string PagerEnvironmentVariable = "PAGER";

    /// <summary>
    ///     The environment variable that controls the complete process locale.
    /// </summary>
    internal const string LocaleEnvironmentVariable = "LC_ALL";

    /// <summary>
    ///     The environment variable that controls the process language.
    /// </summary>
    internal const string LanguageEnvironmentVariable = "LANG";
}
