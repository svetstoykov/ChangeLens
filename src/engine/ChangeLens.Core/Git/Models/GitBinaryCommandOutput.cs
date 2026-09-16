namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents the captured result of one bounded Git process execution with binary standard output.
/// </summary>
/// <param name="ExitCode">The Git process exit code.</param>
/// <param name="StandardOutput">The captured standard output bytes. Cannot be <see langword="null" />.</param>
/// <param name="StandardError">The decoded standard error text. Cannot be <see langword="null" />.</param>
public sealed record GitBinaryCommandOutput(
    int ExitCode,
    byte[] StandardOutput,
    string StandardError);
