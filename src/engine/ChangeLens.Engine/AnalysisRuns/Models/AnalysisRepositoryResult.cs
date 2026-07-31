namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the immutable accepted repository identity in an analysis projection.
/// </summary>
/// <param name="RepositoryId">The stable identifier of the repository.</param>
/// <param name="DisplayName">The human-readable repository name.</param>
/// <param name="CanonicalPath">The canonical local repository path.</param>
/// <param name="Head">The revision at the repository head when the run was accepted.</param>
internal sealed record AnalysisRepositoryResult(string RepositoryId, string DisplayName, string CanonicalPath, string Head);
