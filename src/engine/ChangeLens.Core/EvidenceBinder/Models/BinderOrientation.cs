namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents short repository orientation near disclosed change paths.
/// </summary>
/// <param name="Directories">The bounded directory sibling groups.</param>
public sealed record BinderOrientation(IReadOnlyList<BinderDirectory> Directories);
