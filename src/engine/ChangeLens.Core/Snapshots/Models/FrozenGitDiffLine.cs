namespace ChangeLens.Core.Snapshots.Models;

/// <summary>
///     Represents one changed line from a frozen blob diff.
/// </summary>
/// <param name="LineNumber">The one-based line number on its corresponding blob side.</param>
/// <param name="Content">The line content without the diff marker. Cannot be <see langword="null" />.</param>
public sealed record FrozenGitDiffLine(int LineNumber, string Content);
