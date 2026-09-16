namespace ChangeLens.Core.ChangeAnatomy.Models;

/// <summary>
///     Represents aggregate diagnostics for one change anatomy result.
/// </summary>
/// <param name="ChangedFileCount">The number of manifest entries visited.</param>
/// <param name="AnalyzedFileCount">The number of entries analyzed for keys.</param>
/// <param name="SkippedFileCount">The number of entries skipped with a reason.</param>
/// <param name="TruncatedFileCount">The number of analyzed entries that hit the key cap.</param>
/// <param name="ZeroKeyFileCount">The number of analyzed entries that yielded no keys.</param>
/// <param name="DistinctKeyCount">The number of distinct normalized key and kind pairs.</param>
/// <param name="KeyOccurrenceCount">The number of retained key occurrences.</param>
/// <param name="DistinctKeysByKind">The distinct-key counts by source kind. Cannot be <see langword="null" />.</param>
/// <param name="SkipReasons">The skip counts by human-readable reason. Cannot be <see langword="null" />.</param>
public sealed record ChangeAnatomyDiagnostics(
    int ChangedFileCount,
    int AnalyzedFileCount,
    int SkippedFileCount,
    int TruncatedFileCount,
    int ZeroKeyFileCount,
    int DistinctKeyCount,
    int KeyOccurrenceCount,
    IReadOnlyDictionary<string, int> DistinctKeysByKind,
    IReadOnlyDictionary<string, int> SkipReasons);
