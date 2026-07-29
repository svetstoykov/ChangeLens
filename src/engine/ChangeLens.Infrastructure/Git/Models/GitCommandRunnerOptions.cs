namespace ChangeLens.Infrastructure.Git.Models;

/// <summary>
///     Represents the configurable settings for Git command execution.
/// </summary>
public sealed class GitCommandRunnerOptions
{
    /// <summary>
    ///     Gets or sets the Git executable path or name, or <see langword="null" /> to use the installed Git executable.
    /// </summary>
    public string? ExecutablePath { get; set; }
}
