using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Core.Comparisons.Models;

/// <summary>
///     Represents a freshness check result together with the repository and target identity it resolved.
/// </summary>
/// <param name="State">Whether the prepared facts still match the repository.</param>
/// <param name="Repository">
///     The resolved repository descriptor when <paramref name="State" /> is
///     <see cref="ComparisonFreshnessState.Current" />; otherwise, <see langword="null" />.
/// </param>
/// <param name="TargetRevision">
///     The resolved target revision when <paramref name="State" /> is <see cref="ComparisonFreshnessState.Current" />;
///     otherwise, <see langword="null" />.
/// </param>
public sealed record ComparisonFreshnessCheck(
    ComparisonFreshnessState State,
    RepositoryDescriptor? Repository,
    string? TargetRevision);
