using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Comparisons.Interfaces;

/// <summary>
///     Defines composition of comparison-file summaries.
/// </summary>
public interface IComparisonFileSummaryComposer
{
    /// <summary>
    ///     Composes a summary from committed and working-tree file facts.
    /// </summary>
    /// <param name="records">The file facts to compose. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing distinct lineage and category counts.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="records" /> is <see langword="null" />.
    /// </exception>
    Result<ComparisonFileSummary> Compose(IReadOnlyList<ComparisonFileRecord> records);
}
