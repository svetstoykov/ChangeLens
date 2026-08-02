namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents the immutable repository identity accepted for a run.
/// </summary>
/// <param name="RepositoryId">
///     The retained repository identifier. Cannot be <see langword="null" />.
/// </param>
/// <param name="DisplayName">The repository display name. Cannot be <see langword="null" />.</param>
/// <param name="CanonicalPath">The canonical repository path. Cannot be <see langword="null" />.</param>
/// <param name="CanonicalRepositoryPathKey">
///     The normalized repository key the one-active-run index is keyed on. Cannot be <see langword="null" />.
/// </param>
/// <param name="HeadRevision">The accepted HEAD revision. Cannot be <see langword="null" />.</param>
public sealed record AnalysisRepositoryIdentity(
    Guid RepositoryId,
    string DisplayName,
    string CanonicalPath,
    string CanonicalRepositoryPathKey,
    string HeadRevision);
