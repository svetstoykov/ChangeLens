namespace ChangeLens.Engine.Repositories.Constants;

/// <summary>
///     Provides stable configuration values for repository inspection.
/// </summary>
internal static class RepositoryInspectionConfigurationConstants
{
    /// <summary>
    ///     The configuration key for the Git executable path or name.
    /// </summary>
    internal const string GitExecutableConfigurationKey = "ChangeLens:Repositories:GitExecutable";

    /// <summary>
    ///     The default Git executable resolved from the process search path.
    /// </summary>
    internal const string DefaultGitExecutable = "git";
}
