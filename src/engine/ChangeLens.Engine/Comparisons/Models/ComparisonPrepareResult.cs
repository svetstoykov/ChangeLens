using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Engine.Repositories.Models;

namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents immutable prepared comparison facts in the engine protocol.
/// </summary>
/// <param name="Repository">The inspected repository identity and HEAD.</param>
/// <param name="Target">The exact selected target.</param>
/// <param name="MergeBaseRevision">The unique merge-base revision.</param>
/// <param name="CurrentWorkCommitCount">The number of commits reachable only from the current revision.</param>
/// <param name="TargetOnlyCommitCount">The number of commits reachable only from the selected target.</param>
/// <param name="ChangedFileTotal">The number of distinct changed file lineages.</param>
/// <param name="UncommittedFileTotal">The number of distinct uncommitted file lineages.</param>
/// <param name="StagedFileCount">The number of distinct lineages with staged changes.</param>
/// <param name="UnstagedFileCount">The number of distinct lineages with unstaged changes.</param>
/// <param name="UntrackedFileCount">The number of distinct untracked lineages.</param>
/// <param name="Readiness">The tagged comparison readiness.</param>
/// <param name="FreshnessToken">The deterministic freshness token.</param>
internal sealed record ComparisonPrepareResult(
    RepositoryResult Repository,
    ComparisonTargetResult Target,
    string MergeBaseRevision,
    int CurrentWorkCommitCount,
    int TargetOnlyCommitCount,
    int ChangedFileTotal,
    int UncommittedFileTotal,
    int StagedFileCount,
    int UnstagedFileCount,
    int UntrackedFileCount,
    ComparisonReadinessResult Readiness,
    string FreshnessToken)
{
    /// <summary>
    ///     Maps Core prepared comparison facts to their versioned protocol result.
    /// </summary>
    /// <param name="comparison">The prepared comparison to map. Cannot be <see langword="null" />.</param>
    /// <returns>The prepared-comparison protocol result.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="comparison" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     The comparison contains an unapproved readiness value.
    /// </exception>
    internal static ComparisonPrepareResult FromPreparedComparison(PreparedComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        ComparisonReadinessResult readiness = comparison.Readiness switch
        {
            ComparisonReadiness.Ready => new ReadyComparisonReadinessResult(),
            ComparisonReadiness.Empty => new EmptyComparisonReadinessResult(),
            ComparisonReadiness.Conflicts => new ConflictsComparisonReadinessResult(
                comparison.Files.ConflictedFileCount),
            _ => throw new InvalidOperationException(
                "The comparison readiness is not approved for the engine protocol."),
        };

        return new ComparisonPrepareResult(
            RepositoryResult.FromDescriptor(comparison.Repository),
            ComparisonTargetResult.FromDescriptor(comparison.Target),
            comparison.MergeBaseRevision,
            comparison.CurrentWorkCommitCount,
            comparison.TargetOnlyCommitCount,
            comparison.Files.ChangedFileTotal,
            comparison.Files.UncommittedFileTotal,
            comparison.Files.StagedFileCount,
            comparison.Files.UnstagedFileCount,
            comparison.Files.UntrackedFileCount,
            readiness,
            comparison.FreshnessToken);
    }
}
