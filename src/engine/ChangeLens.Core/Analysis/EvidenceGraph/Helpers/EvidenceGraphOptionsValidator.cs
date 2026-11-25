using ChangeLens.Core.Analysis.EvidenceGraph.Models;

namespace ChangeLens.Core.Analysis.EvidenceGraph.Helpers;

/// <summary>
///     Validates configured bounds and shares for evidence graph construction.
/// </summary>
public static class EvidenceGraphOptionsValidator
{
    /// <summary>
    ///     Validates evidence graph bounds and node-share limits.
    /// </summary>
    /// <param name="options">The evidence graph options to validate. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Any bound or share violates its invariant.</exception>
    public static void Validate(EvidenceGraphOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumQuoteWindowNodes <= 0
            || options.MaximumQuotedLinesPerNode <= 0
            || options.CandidateWindowLines <= 0
            || options.MaximumNodesPerChangedFile <= 0
            || options.MaximumNodesPerCandidate <= 0
            || options.MaximumCandidateHeadNodes <= 0
            || options.MaximumEdges <= 0
            || options.MaximumEdgesPerNodePair <= 0
            || options.HunkContextLines < 0
            || options.MaximumWholeChangedFileLines < 0
            || options.MaximumBlockExtensionLines < 0
            || options.MergeGapLines < 0
            || options.ChangedNodeShare < 0
            || options.ChangedNodeShare > 1)
        {
            throw new InvalidOperationException("Evidence graph options must contain valid bounds and shares.");
        }
    }
}
