using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents one changed manifest entry and its disclosed evidence-node addresses.
/// </summary>
/// <param name="Path">The current changed path.</param>
/// <param name="OriginalPath">The before-side path for a rename, or <see langword="null" />.</param>
/// <param name="Category">The captured change category.</param>
/// <param name="BeforeMode">The merge-base tree-entry mode.</param>
/// <param name="AfterMode">The HEAD tree-entry mode.</param>
/// <param name="EvidenceNodeIds">The disclosed node ids attached to this entry.</param>
public sealed record BinderChangedFile(
    string Path,
    string? OriginalPath,
    SnapshotChangeCategory Category,
    string BeforeMode,
    string AfterMode,
    IReadOnlyList<string> EvidenceNodeIds);
