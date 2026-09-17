using ChangeLens.Core.EvidenceGraph.Models;

namespace ChangeLens.Core.EvidenceGraph.Helpers;

/// <summary>
///     Selects changed-file windows by round-robin across files, resuming from the current selection.
/// </summary>
internal sealed class EvidenceGraphChangedSelection
{
    private readonly IReadOnlyList<EvidenceGraphChangedWindowQueue> _queues;
    private readonly List<EvidenceGraphWindow> _selected = [];

    /// <summary>
    ///     Initializes the selector over the changed files in selection order.
    /// </summary>
    /// <param name="queues">The per-file window queues in file order. Cannot be <see langword="null" />.</param>
    internal EvidenceGraphChangedSelection(IReadOnlyList<EvidenceGraphChangedWindowQueue> queues) => this._queues = queues;

    /// <summary>
    ///     Gets the windows selected so far, in selection order.
    /// </summary>
    internal IReadOnlyList<EvidenceGraphWindow> Selected => this._selected;

    /// <summary>
    ///     Gets the number of windows selected so far.
    /// </summary>
    internal int SelectedCount => this._selected.Count;

    /// <summary>
    ///     Takes one more window per file per pass until the selection reaches the limit or a full pass takes nothing.
    /// </summary>
    /// <param name="limit">The maximum total windows to select across all files.</param>
    internal void Run(int limit)
    {
        while (this.SelectedCount < limit)
        {
            var tookWindow = false;
            foreach (var queue in this._queues)
            {
                if (this.SelectedCount >= limit)
                {
                    break;
                }

                var window = queue.TakeNext();
                if (window is null)
                {
                    continue;
                }

                this._selected.Add(window);
                tookWindow = true;
            }

            if (!tookWindow)
            {
                break;
            }
        }
    }
}
