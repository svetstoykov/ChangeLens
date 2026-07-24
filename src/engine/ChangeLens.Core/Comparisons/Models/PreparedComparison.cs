using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Core.Comparisons.Models;

/// <summary>
///     Represents immutable facts for a prepared comparison.
/// </summary>
/// <param name="Repository">The inspected repository identity and HEAD. Cannot be <see langword="null" />.</param>
/// <param name="Target">The exact selected comparison target. Cannot be <see langword="null" />.</param>
/// <param name="MergeBaseRevision">The unique merge-base revision. Cannot be <see langword="null" />.</param>
/// <param name="CurrentWorkCommitCount">The number of commits reachable only from the current revision.</param>
/// <param name="TargetOnlyCommitCount">The number of commits reachable only from the selected target.</param>
/// <param name="Files">The distinct file summary. Cannot be <see langword="null" />.</param>
/// <param name="Readiness">The comparison presentation readiness.</param>
/// <param name="FreshnessToken">The deterministic freshness token. Cannot be <see langword="null" />.</param>
public sealed record PreparedComparison(
    RepositoryDescriptor Repository,
    ComparisonTargetDescriptor Target,
    string MergeBaseRevision,
    int CurrentWorkCommitCount,
    int TargetOnlyCommitCount,
    ComparisonFileSummary Files,
    ComparisonReadiness Readiness,
    string FreshnessToken);
