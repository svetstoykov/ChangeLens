using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents the comparison identity and excluded capture metadata sent to the curator.
/// </summary>
/// <param name="RunId">The accepted analysis run identifier.</param>
/// <param name="RepositoryKey">The canonical repository path key.</param>
/// <param name="Target">The selected comparison target.</param>
/// <param name="TargetRevision">The captured target revision.</param>
/// <param name="HeadRevision">The captured HEAD revision.</param>
/// <param name="MergeBaseRevision">The captured merge-base revision.</param>
/// <param name="CapturedAtUnixMilliseconds">The capture timestamp.</param>
/// <param name="ExcludedUncommittedCounts">The excluded uncommitted lineage counts.</param>
public sealed record BinderComparison(
    Guid RunId,
    string RepositoryKey,
    string Target,
    string TargetRevision,
    string HeadRevision,
    string MergeBaseRevision,
    long CapturedAtUnixMilliseconds,
    ExcludedUncommittedCounts ExcludedUncommittedCounts);
