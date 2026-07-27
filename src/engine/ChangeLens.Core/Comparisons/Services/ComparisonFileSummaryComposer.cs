using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Comparisons.Services;

/// <summary>
///     Composes distinct comparison-file lineages and overlapping working-tree categories.
/// </summary>
/// <remarks>
///     The service is stateless and thread-safe and can be registered with any dependency-injection lifetime.
/// </remarks>
public sealed class ComparisonFileSummaryComposer : IComparisonFileSummaryComposer
{
    /// <inheritdoc />
    public Result<ComparisonFileSummary> Compose(
        IReadOnlyList<ComparisonFileRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var parents = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var record in records)
        {
            AddPath(parents, record.Path);

            if (record.OriginalPath is not null)
            {
                AddPath(parents, record.OriginalPath);
                Union(parents, record.OriginalPath, record.Path);
            }
        }

        try
        {
            return Result.Success(
                new ComparisonFileSummary(
                    CountDistinctRoots(records, parents, static _ => true),
                    CountDistinctRoots(records, parents, static record => !record.IsCommitted),
                    CountDistinctRoots(records, parents, static record => record.IsStaged),
                    CountDistinctRoots(records, parents, static record => record.IsUnstaged),
                    CountDistinctRoots(records, parents, static record => record.IsUntracked),
                    CountDistinctRoots(records, parents, static record => record.IsConflicted)));
        }
        catch (OverflowException)
        {
            return OperationError.UnprocessableInput(
                "The comparison exceeds the supported local inspection limit.",
                ComparisonErrorCode.TooLarge);
        }
    }

    /// <summary>
    ///     Adds a path as its own lineage root when it has not been observed.
    /// </summary>
    /// <param name="parents">The mutable lineage parent map.</param>
    /// <param name="path">The path to add.</param>
    private static void AddPath(
        IDictionary<string, string> parents,
        string path)
    {
        if (!parents.ContainsKey(path))
        {
            parents.Add(path, path);
        }
    }

    /// <summary>
    ///     Finds and compresses the lineage root for a path.
    /// </summary>
    /// <param name="parents">The mutable lineage parent map.</param>
    /// <param name="path">The path whose root is resolved.</param>
    /// <returns>The canonical root selected for the path's lineage.</returns>
    private static string Find(
        IDictionary<string, string> parents,
        string path)
    {
        var root = path;

        while (!StringComparer.Ordinal.Equals(parents[root], root))
        {
            root = parents[root];
        }

        while (!StringComparer.Ordinal.Equals(parents[path], path))
        {
            var parent = parents[path];
            parents[path] = root;
            path = parent;
        }

        return root;
    }

    /// <summary>
    ///     Joins two path lineages through a committed or working-tree rename edge.
    /// </summary>
    /// <param name="parents">The mutable lineage parent map.</param>
    /// <param name="left">The original side of the rename edge.</param>
    /// <param name="right">The current side of the rename edge.</param>
    private static void Union(
        IDictionary<string, string> parents,
        string left,
        string right)
    {
        var leftRoot = Find(parents, left);
        var rightRoot = Find(parents, right);

        if (!StringComparer.Ordinal.Equals(leftRoot, rightRoot))
        {
            parents[rightRoot] = leftRoot;
        }
    }

    /// <summary>
    ///     Counts unique lineage roots whose records match one summary category.
    /// </summary>
    /// <param name="records">The file facts to inspect.</param>
    /// <param name="parents">The mutable lineage parent map.</param>
    /// <param name="predicate">The category predicate applied to each record.</param>
    /// <returns>The checked number of unique matching lineage roots.</returns>
    private static int CountDistinctRoots(
        IEnumerable<ComparisonFileRecord> records,
        IDictionary<string, string> parents,
        Func<ComparisonFileRecord, bool> predicate)
    {
        var roots = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;

        foreach (var record in records.Where(predicate))
        {
            if (roots.Add(Find(parents, record.Path)))
            {
                count = checked(count + 1);
            }
        }

        return count;
    }
}
