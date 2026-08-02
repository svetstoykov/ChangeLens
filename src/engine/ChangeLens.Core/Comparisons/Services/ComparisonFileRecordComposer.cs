using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Git.Models;

namespace ChangeLens.Core.Comparisons.Services;

/// <summary>
///     Converts parsed committed and non-ignored working-tree Git facts into lineage-composition records.
/// </summary>
internal static class ComparisonFileRecordComposer
{
    /// <summary>Composes the records a file-summary composer needs from both Git fact sources.</summary>
    /// <param name="committedFiles">The parsed committed file facts. Cannot be <see langword="null" />.</param>
    /// <param name="workingTree">The parsed working-tree facts. Cannot be <see langword="null" />.</param>
    /// <returns>The file records used for lineage and category composition.</returns>
    internal static IReadOnlyList<ComparisonFileRecord> Compose(
        IReadOnlyList<GitComparisonFileRecord> committedFiles,
        IReadOnlyList<GitWorkingTreeRecord> workingTree)
    {
        var records = new List<ComparisonFileRecord>(committedFiles.Count + workingTree.Count);
        records.AddRange(committedFiles.Select(record => new ComparisonFileRecord(record.Path, record.OriginalPath, true,
            false, false, false, false)));
        records.AddRange(workingTree.Where(record => !record.IsIgnored).Select(record => new ComparisonFileRecord(
            record.Path, record.OriginalPath, false, record.IsStaged, record.IsUnstaged, record.IsUntracked,
            record.IsConflicted)));
        return records;
    }
}
