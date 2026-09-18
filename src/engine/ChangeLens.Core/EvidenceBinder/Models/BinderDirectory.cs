namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents bounded sibling paths for one changed directory.
/// </summary>
/// <param name="Path">The directory path.</param>
/// <param name="Paths">The retained sibling paths.</param>
/// <param name="OmittedCount">The number of sibling paths omitted by the orientation cap.</param>
public sealed record BinderDirectory(string Path, IReadOnlyList<string> Paths, int OmittedCount);
