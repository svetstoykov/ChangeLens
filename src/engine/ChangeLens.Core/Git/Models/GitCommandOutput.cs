namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents the captured result of one bounded Git process execution.
/// </summary>
/// <param name="ExitCode">The Git process exit code.</param>
/// <param name="StandardOutput">The decoded standard output text. Cannot be <see langword="null" />.</param>
/// <param name="StandardError">The decoded standard error text. Cannot be <see langword="null" />.</param>
public sealed record GitCommandOutput(
    int ExitCode,
    string StandardOutput,
    string StandardError);
