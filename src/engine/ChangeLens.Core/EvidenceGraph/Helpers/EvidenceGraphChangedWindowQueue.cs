using ChangeLens.Core.EvidenceGraph.Models;

namespace ChangeLens.Core.EvidenceGraph.Helpers;

/// <summary>
///     Represents the remaining interleaved windows of one changed file during round-robin selection.
/// </summary>
internal sealed class EvidenceGraphChangedWindowQueue
{
    private readonly Queue<EvidenceGraphWindow> _queue;

    /// <summary>
    ///     Initializes a queue for one changed file.
    /// </summary>
    /// <param name="ordered">The windows in selection order. Cannot be <see langword="null" />.</param>
    /// <param name="maximumPerFile">The maximum windows the file may contribute.</param>
    internal EvidenceGraphChangedWindowQueue(IEnumerable<EvidenceGraphWindow> ordered, int maximumPerFile)
    {
        this._queue = new Queue<EvidenceGraphWindow>(ordered);
        this.MaximumPerFile = maximumPerFile;
    }

    /// <summary>
    ///     Gets the maximum windows the file may contribute.
    /// </summary>
    internal int MaximumPerFile { get; }

    /// <summary>
    ///     Gets the number of windows already taken from the queue.
    /// </summary>
    internal int SelectedCount { get; private set; }

    /// <summary>
    ///     Takes the next window when the file is under its cap and windows remain.
    /// </summary>
    /// <returns>The next window, or <see langword="null" /> when the file is capped or exhausted.</returns>
    internal EvidenceGraphWindow? TakeNext()
    {
        if (this.SelectedCount >= this.MaximumPerFile || this._queue.Count == 0)
        {
            return null;
        }

        this.SelectedCount++;
        return this._queue.Dequeue();
    }
}
