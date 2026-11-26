using System.Collections;

namespace ChangeLens.Engine.IntegrationTests.Analysis.EvidenceBinder.Support;

/// <summary>
///     Wraps a fixed list, counts each enumeration, and runs a callback when an enumeration starts.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="items">The items to expose.</param>
/// <param name="onEnumerate">The callback run each time an enumeration starts.</param>
public sealed class EnumerationCountingList<T>(IReadOnlyList<T> items, Action onEnumerate) : IReadOnlyList<T>
{
    /// <summary>
    ///     Gets the number of enumerations started so far.
    /// </summary>
    public int EnumerationCount { get; private set; }

    /// <inheritdoc />
    public int Count => items.Count;

    /// <inheritdoc />
    public T this[int index] => items[index];

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        this.EnumerationCount++;
        onEnumerate();
        return items.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
