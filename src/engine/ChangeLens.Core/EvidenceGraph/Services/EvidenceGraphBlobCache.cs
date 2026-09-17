using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;

namespace ChangeLens.Core.EvidenceGraph.Services;

/// <summary>
///     Caches frozen blob reads for one evidence graph build and records content skips once per object id.
/// </summary>
internal sealed class EvidenceGraphBlobCache
{
    private readonly IFrozenGitTreeReader _reader;
    private readonly IDictionary<string, int> _skipReasons;
    private readonly Dictionary<string, FrozenGitBlob> _blobs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _countedSkips = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a cache over one snapshot-scoped reader.
    /// </summary>
    /// <param name="reader">The frozen-tree reader for the build. Cannot be <see langword="null" />.</param>
    /// <param name="skipReasons">The shared skip-reason counts. Cannot be <see langword="null" />.</param>
    internal EvidenceGraphBlobCache(IFrozenGitTreeReader reader, IDictionary<string, int> skipReasons)
    {
        this._reader = reader;
        this._skipReasons = skipReasons;
    }

    /// <summary>
    ///     Asynchronously reads a captured blob, reusing the cached read when the object was already read.
    /// </summary>
    /// <param name="objectId">The captured blob object identifier. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task.</param>
    /// <returns>A task whose result contains the blob text, a content skip, or a frozen-read failure.</returns>
    internal async Task<Result<FrozenGitBlob>> ReadAsync(string objectId, CancellationToken cancellationToken)
    {
        if (this._blobs.TryGetValue(objectId, out var cached))
        {
            return cached;
        }

        var result = await this._reader.ReadBlobAsync(objectId, cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        var blob = result.Data!;
        this._blobs[objectId] = blob;
        if (!blob.HasText && this._countedSkips.Add(objectId))
        {
            Count(this._skipReasons, DescribeSkip(blob.SkipReason));
        }

        return blob;
    }

    /// <summary>
    ///     Gets a blob that was already read for this build.
    /// </summary>
    /// <param name="objectId">The captured blob object identifier. Cannot be <see langword="null" />.</param>
    /// <returns>The cached blob.</returns>
    internal FrozenGitBlob Get(string objectId) => this._blobs[objectId];

    /// <summary>
    ///     Returns the short skip reason for a content skip.
    /// </summary>
    /// <param name="reason">The frozen-read skip reason.</param>
    /// <returns>The short skip reason recorded in the graph diagnostics.</returns>
    internal static string DescribeSkip(FrozenGitBlobSkipReason reason) =>
        reason is FrozenGitBlobSkipReason.TooLarge ? "oversized content" : "binary content";

    /// <summary>
    ///     Increments a count in a skip-reason dictionary.
    /// </summary>
    /// <param name="counts">The counts to update. Cannot be <see langword="null" />.</param>
    /// <param name="reason">The short skip reason. Cannot be <see langword="null" />.</param>
    internal static void Count(IDictionary<string, int> counts, string reason) =>
        counts[reason] = counts.TryGetValue(reason, out var count) ? count + 1 : 1;
}
